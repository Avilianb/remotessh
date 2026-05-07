using System.Security.Cryptography;
using System.Text;
using MySSH.Models;

namespace MySSH.Services;

public sealed class HostKeyService
{
    private readonly OpenSshTools _tools;
    private readonly ProcessRunner _runner;

    public HostKeyService(OpenSshTools tools, ProcessRunner runner)
    {
        _tools = tools;
        _runner = runner;
    }

    public bool IsTrusted(ServerProfile server)
    {
        if (!File.Exists(AppPaths.KnownHostsPath))
        {
            return false;
        }

        var knownHost = FormatKnownHost(server.Host, server.Port);
        var plainHost = server.Host;
        foreach (var line in File.ReadLines(AppPaths.KnownHostsPath))
        {
            var hostField = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (hostField is null || hostField.StartsWith('|'))
            {
                continue;
            }

            var hosts = hostField.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (hosts.Any(host => string.Equals(host, knownHost, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(host, plainHost, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<HostKeyScanResult> ScanAsync(ServerProfile server, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
            _tools.SshKeyscanPath,
            ["-T", "10", "-p", server.Port.ToString(), server.Host],
            TimeSpan.FromSeconds(15),
            cancellationToken: cancellationToken);

        var keyLines = result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .ToList();

        if (result.ExitCode != 0 || keyLines.Count == 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? "Could not scan host key."
                : result.StandardError.Trim();
            return new HostKeyScanResult(false, "", "", message);
        }

        var fingerprint = ComputeFingerprint(keyLines[0]);
        return new HostKeyScanResult(true, string.Join(Environment.NewLine, keyLines), fingerprint, "");
    }

    public async Task TrustAsync(ServerProfile server, string scannedKeys, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();

        var lines = scannedKeys
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => RewriteKnownHostPrefix(line, server))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (lines.Count == 0)
        {
            return;
        }

        await File.AppendAllTextAsync(
            AppPaths.KnownHostsPath,
            string.Join(Environment.NewLine, lines) + Environment.NewLine,
            Encoding.UTF8,
            cancellationToken);
    }

    private static string RewriteKnownHostPrefix(string keyscanLine, ServerProfile server)
    {
        var firstSpace = keyscanLine.IndexOf(' ');
        if (firstSpace <= 0)
        {
            return keyscanLine;
        }

        return $"{FormatKnownHost(server.Host, server.Port)}{keyscanLine[firstSpace..]}";
    }

    private static string FormatKnownHost(string host, int port)
    {
        return port == 22 ? host : $"[{host}]:{port}";
    }

    private static string ComputeFingerprint(string keyLine)
    {
        var parts = keyLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return "unknown";
        }

        try
        {
            var keyBytes = Convert.FromBase64String(parts[2]);
            var hash = SHA256.HashData(keyBytes);
            return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
        }
        catch
        {
            return "unknown";
        }
    }
}

public sealed record HostKeyScanResult(
    bool Success,
    string KeyLines,
    string Fingerprint,
    string ErrorMessage);
