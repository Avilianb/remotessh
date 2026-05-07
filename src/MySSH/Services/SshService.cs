using System.Net.Sockets;
using MySSH.Models;

namespace MySSH.Services;

public sealed class SshService
{
    private readonly OpenSshTools _tools;
    private readonly ProcessRunner _runner;

    public SshService(OpenSshTools tools, ProcessRunner runner)
    {
        _tools = tools;
        _runner = runner;
    }

    public async Task<bool> ProbePortAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(
        ServerProfile server,
        string? keyPath = null,
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        const string command = "printf 'MYSSH_OK\\n'; " +
                               "uname -srm 2>/dev/null || true; " +
                               "(getconf _NPROCESSORS_ONLN 2>/dev/null || nproc 2>/dev/null || true); " +
                               "(awk '/MemTotal/ {print $2 \" kB\"}' /proc/meminfo 2>/dev/null || true); " +
                               "(df -h --total / 2>/dev/null | awk 'END {print $2}' || true)";

        var result = await RunCommandAsync(server, command, keyPath, port, TimeSpan.FromSeconds(20), cancellationToken);
        if (result.ExitCode != 0)
        {
            var message = result.TimedOut
                ? "Connection timed out."
                : FirstUsefulLine(result.StandardError, result.StandardOutput, "Connection failed.");
            return new ConnectionTestResult(false, message, new MachineSnapshot());
        }

        var lines = result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (lines.Count == 0 || !lines[0].Contains("MYSSH_OK", StringComparison.Ordinal))
        {
            return new ConnectionTestResult(false, "SSH command completed without the expected verification marker.", new MachineSnapshot());
        }

        var snapshot = new MachineSnapshot
        {
            Os = lines.ElementAtOrDefault(1) ?? "",
            CpuCores = lines.ElementAtOrDefault(2) ?? "",
            MemoryTotal = lines.ElementAtOrDefault(3) ?? "",
            DiskTotal = lines.ElementAtOrDefault(4) ?? ""
        };

        return new ConnectionTestResult(true, "Connection verified.", snapshot);
    }

    public Task<ProcessResult> RunCommandAsync(
        ServerProfile server,
        string command,
        string? keyPath,
        int? port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var args = BuildBaseArgs(server, keyPath, port, batchMode: true);
        args.Add(command);

        return _runner.RunAsync(_tools.SshPath, args, timeout, cancellationToken: cancellationToken);
    }

    public List<string> BuildBaseArgs(ServerProfile server, string? keyPath, int? port, bool batchMode)
    {
        var args = new List<string>
        {
            "-o", "ConnectTimeout=10",
            "-o", "ServerAliveInterval=15",
            "-o", "StrictHostKeyChecking=yes",
            "-p", (port ?? server.Port).ToString()
        };

        if (batchMode)
        {
            args.Add("-o");
            args.Add("BatchMode=yes");
        }

        var selectedKey = string.IsNullOrWhiteSpace(keyPath) ? server.KeyPath : keyPath;
        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            args.Add("-i");
            args.Add(selectedKey);
        }

        args.Add($"{server.User}@{server.Host}");
        return args;
    }

    private static string FirstUsefulLine(params string[] values)
    {
        foreach (var value in values)
        {
            var line = value
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return "Unknown error.";
    }
}
