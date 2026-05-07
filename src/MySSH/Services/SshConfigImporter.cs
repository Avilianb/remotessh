using MySSH.Models;

namespace MySSH.Services;

public sealed class SshConfigImporter
{
    public async Task<IReadOnlyList<SshConfigEntry>> ReadDefaultConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(AppPaths.SshConfigPath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(AppPaths.SshConfigPath, cancellationToken);
        return Parse(lines);
    }

    public IReadOnlyList<SshConfigEntry> Parse(IReadOnlyList<string> lines)
    {
        var entries = new List<SshConfigEntry>();
        var aliases = new List<string>();
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hostLine = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = StripComment(lines[i]).Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var keyAndValue = SplitDirective(trimmed);
            if (keyAndValue is null)
            {
                continue;
            }

            var (key, value) = keyAndValue.Value;
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                aliases = value
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(IsImportableAlias)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                hostLine = i + 1;
                continue;
            }

            if (aliases.Count > 0)
            {
                properties[key] = Unquote(value);
            }
        }

        Flush();
        return entries;

        void Flush()
        {
            if (aliases.Count == 0)
            {
                properties.Clear();
                return;
            }

            properties.TryGetValue("HostName", out var hostName);
            properties.TryGetValue("User", out var user);
            properties.TryGetValue("Port", out var portText);
            properties.TryGetValue("IdentityFile", out var identityFile);
            _ = int.TryParse(portText, out var port);

            foreach (var alias in aliases)
            {
                entries.Add(new SshConfigEntry
                {
                    Alias = alias,
                    HostName = ExpandPath(hostName ?? alias),
                    User = user ?? "",
                    Port = port <= 0 ? 22 : port,
                    IdentityFile = ExpandPath(identityFile ?? ""),
                    SourceLine = hostLine
                });
            }

            aliases.Clear();
            properties.Clear();
        }
    }

    private static bool IsImportableAlias(string alias)
    {
        return !alias.Contains('*') && !alias.Contains('?') && !alias.StartsWith("!", StringComparison.Ordinal);
    }

    private static (string Key, string Value)? SplitDirective(string line)
    {
        var firstSpace = line.IndexOfAny([' ', '\t']);
        if (firstSpace < 0)
        {
            return null;
        }

        var key = line[..firstSpace].Trim();
        var value = line[firstSpace..].Trim();
        return key.Length == 0 || value.Length == 0 ? null : (key, value);
    }

    private static string StripComment(string line)
    {
        var inQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuote = !inQuote;
            }

            if (!inQuote && line[i] == '#')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
    }

    private static string ExpandPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        value = Unquote(value);
        if (value.StartsWith("~/", StringComparison.Ordinal) || value.StartsWith("~\\", StringComparison.Ordinal))
        {
            value = Path.Combine(AppPaths.UserProfile, value[2..]);
        }

        return Environment.ExpandEnvironmentVariables(value);
    }
}
