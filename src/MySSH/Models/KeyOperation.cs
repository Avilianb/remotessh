namespace MySSH.Models;

public sealed class KeyOperation
{
    public string Token { get; init; } = "";

    public int TemporaryPort { get; init; }

    public string PrivateKeyPath { get; init; } = "";

    public string PublicKeyPath { get; init; } = "";

    public string PublicKey { get; init; } = "";

    public string BootstrapCommand { get; init; } = "";

    public bool TemporaryPortOpen { get; set; }

    public bool TemporaryKeyVerified { get; set; }

    public bool OfficialKeyVerified { get; set; }

    public bool TemporaryEntryClosed { get; set; }

    public string Mode { get; init; } = "takeover";

    public string MaskedToken => Token.Length <= 8
        ? "********"
        : $"{Token[..4]}...{Token[^4..]}";
}
