using System.Text;
using MySSH.Models;

namespace MySSH.Services;

public sealed class LocalSshConfigService
{
    private const string BeginMarkerPrefix = "# BEGIN My SSH ";
    private const string EndMarkerPrefix = "# END My SSH ";

    public async Task UpsertManagedHostAsync(ServerProfile server, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();

        var lines = File.Exists(AppPaths.SshConfigPath)
            ? (await File.ReadAllLinesAsync(AppPaths.SshConfigPath, cancellationToken)).ToList()
            : [];

        EnsureNoExternalAliasConflict(lines, server.Alias);
        RemoveManagedBlock(lines, server.Alias);

        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.Add("");
        }

        lines.AddRange(BuildManagedBlock(server));
        await File.WriteAllLinesAsync(AppPaths.SshConfigPath, lines, Encoding.UTF8, cancellationToken);
    }

    private static void EnsureNoExternalAliasConflict(IReadOnlyList<string> lines, string alias)
    {
        var isManagedBlock = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith(BeginMarkerPrefix, StringComparison.Ordinal))
            {
                isManagedBlock = true;
                continue;
            }

            if (trimmed.StartsWith(EndMarkerPrefix, StringComparison.Ordinal))
            {
                isManagedBlock = false;
                continue;
            }

            if (isManagedBlock || !trimmed.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var aliases = trimmed[5..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (aliases.Any(candidate => string.Equals(candidate, alias, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"SSH config already contains a non-My SSH Host alias named '{alias}' at line {i + 1}.");
            }
        }
    }

    private static void RemoveManagedBlock(List<string> lines, string alias)
    {
        var beginMarker = $"{BeginMarkerPrefix}{alias}";
        var endMarker = $"{EndMarkerPrefix}{alias}";
        var start = lines.FindIndex(line => string.Equals(line.Trim(), beginMarker, StringComparison.Ordinal));
        if (start < 0)
        {
            return;
        }

        var end = lines.FindIndex(start, line => string.Equals(line.Trim(), endMarker, StringComparison.Ordinal));
        if (end < start)
        {
            end = start;
        }

        lines.RemoveRange(start, end - start + 1);
    }

    private static IEnumerable<string> BuildManagedBlock(ServerProfile server)
    {
        yield return $"{BeginMarkerPrefix}{server.Alias}";
        yield return $"Host {server.Alias}";
        yield return $"    HostName {server.Host}";
        yield return $"    User {server.User}";
        yield return $"    Port {server.Port}";

        if (!string.IsNullOrWhiteSpace(server.KeyPath))
        {
            yield return $"    IdentityFile {QuoteForSshConfig(server.KeyPath)}";
            yield return "    IdentitiesOnly yes";
        }

        yield return $"{EndMarkerPrefix}{server.Alias}";
    }

    private static string QuoteForSshConfig(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains(' ') ? $"\"{normalized}\"" : normalized;
    }
}
