using System.Text.Json;
using MySSH.Models;

namespace MySSH.Services;

public sealed class ServerStore
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();

        if (!File.Exists(AppPaths.ServerStorePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(AppPaths.ServerStorePath);
        var servers = await JsonSerializer.DeserializeAsync<List<ServerProfile>>(stream, _jsonOptions, cancellationToken);
        return servers ?? [];
    }

    public async Task SaveAsync(IEnumerable<ServerProfile> servers, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();

        var safeServers = servers
            .OrderBy(server => server.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var stream = File.Create(AppPaths.ServerStorePath);
        await JsonSerializer.SerializeAsync(stream, safeServers, _jsonOptions, cancellationToken);
    }
}
