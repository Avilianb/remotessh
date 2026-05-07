using System.Net.Http;
using System.Reflection;

namespace MySSH.Services;

public sealed class UpdateCheckService
{
    private const string AppReleaseUrl = "";
    private const string ScriptReleaseUrl = "https://github.com/Avilianb/remotessh/releases/tag/v0.1.0";

    public string CurrentAppVersion =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    public async Task<string> CheckAppAsync(CancellationToken cancellationToken = default)
    {
        if (!TryCreateUri(AppReleaseUrl, out var releaseUri))
        {
            return $"Current app version: {CurrentAppVersion}. No app release URL is configured yet.";
        }

        return await ProbeAsync(releaseUri, $"Current app version: {CurrentAppVersion}", cancellationToken);
    }

    public async Task<string> CheckScriptAsync(CancellationToken cancellationToken = default)
    {
        if (!TryCreateUri(ScriptReleaseUrl, out var releaseUri))
        {
            return $"Configured script release: {KeyManagementService.ScriptReleaseTag}. No script release URL is configured yet.";
        }

        return await ProbeAsync(releaseUri, $"Configured script release: {KeyManagementService.ScriptReleaseTag}", cancellationToken);
    }

    private static bool TryCreateUri(string value, out Uri uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri!);
    }

    private static async Task<string> ProbeAsync(Uri uri, string prefix, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Head, uri);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            return $"{prefix}. Release endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}.";
        }
        catch (Exception ex)
        {
            return $"{prefix}. Update check failed: {ex.Message}";
        }
    }
}
