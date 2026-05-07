namespace MySSH.Services;

public sealed class OperationLogger
{
    public async Task AppendAsync(string serverAlias, string step, string message, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();
        var line = $"{DateTimeOffset.Now:O}\t{Sanitize(serverAlias)}\t{Sanitize(step)}\t{Sanitize(message)}{Environment.NewLine}";
        await File.AppendAllTextAsync(AppPaths.OperationLogPath, line, cancellationToken);
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
    }
}
