namespace MySSH.Models;

public sealed class SshConfigEntry
{
    public string Alias { get; init; } = "";

    public string HostName { get; init; } = "";

    public int Port { get; init; } = 22;

    public string User { get; init; } = "";

    public string IdentityFile { get; init; } = "";

    public int SourceLine { get; init; }

    public ServerProfile ToServerProfile()
    {
        return new ServerProfile
        {
            Alias = Alias,
            Host = string.IsNullOrWhiteSpace(HostName) ? Alias : HostName,
            Port = Port <= 0 ? 22 : Port,
            User = User,
            KeyPath = IdentityFile,
            Tags = ["imported"],
            Notes = $"Imported from SSH config line {SourceLine}."
        };
    }
}
