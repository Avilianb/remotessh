using System.Text.Json.Serialization;

namespace MySSH.Models;

public sealed class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Alias { get; set; } = "";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 22;

    public string User { get; set; } = "";

    public string KeyPath { get; set; } = "";

    public List<string> Tags { get; set; } = [];

    public string Notes { get; set; } = "";

    public string LastConnectionStatus { get; set; } = "未测试";

    public DateTimeOffset? LastConnectedAtUtc { get; set; }

    public MachineSnapshot Machine { get; set; } = new();

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public string TagsText
    {
        get => string.Join(", ", Tags);
        set => Tags = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [JsonIgnore]
    public string Endpoint => Port == 22 ? Host : $"{Host}:{Port}";

    public ServerProfile Clone()
    {
        return new ServerProfile
        {
            Id = Id,
            Alias = Alias,
            Host = Host,
            Port = Port,
            User = User,
            KeyPath = KeyPath,
            Tags = [.. Tags],
            Notes = Notes,
            LastConnectionStatus = LastConnectionStatus,
            LastConnectedAtUtc = LastConnectedAtUtc,
            Machine = new MachineSnapshot
            {
                Os = Machine.Os,
                CpuCores = Machine.CpuCores,
                MemoryTotal = Machine.MemoryTotal,
                DiskTotal = Machine.DiskTotal
            },
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc
        };
    }

    public void CopyEditableFieldsFrom(ServerProfile source)
    {
        Alias = source.Alias;
        Host = source.Host;
        Port = source.Port;
        User = source.User;
        KeyPath = source.KeyPath;
        Tags = [.. source.Tags];
        Notes = source.Notes;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
