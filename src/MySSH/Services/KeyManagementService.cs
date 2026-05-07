using System.Security.Cryptography;
using System.Text;
using MySSH.Models;

namespace MySSH.Services;

public sealed class KeyManagementService
{
    public const string ScriptReleaseTag = "v0.1.0";
    public const string ScriptDeliveryMode = "github-release";
    public const string ScriptReleaseBaseUrl = "https://github.com/Avilianb/remotessh/releases/download/v0.1.0";
    public const string BootstrapScriptUrl = ScriptReleaseBaseUrl + "/myssh-bootstrap.sh";
    public const string CleanupScriptUrl = ScriptReleaseBaseUrl + "/myssh-cleanup.sh";
    public const string DisablePasswordScriptUrl = ScriptReleaseBaseUrl + "/myssh-disable-password.sh";

    private readonly OpenSshTools _tools;
    private readonly ProcessRunner _runner;

    public KeyManagementService(OpenSshTools tools, ProcessRunner runner)
    {
        _tools = tools;
        _runner = runner;
    }

    public async Task<KeyOperation> PrepareOperationAsync(
        ServerProfile server,
        bool rotate,
        bool overwriteExisting,
        CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();

        var safeAlias = MakeSafeFileName(server.Alias);
        var suffix = rotate ? "_next_ed25519" : "_ed25519";
        var privateKeyPath = Path.Combine(AppPaths.ManagedKeysRoot, $"{safeAlias}{suffix}");
        var publicKeyPath = $"{privateKeyPath}.pub";

        if (File.Exists(privateKeyPath) || File.Exists(publicKeyPath))
        {
            if (!overwriteExisting)
            {
                throw new InvalidOperationException($"Key already exists at {privateKeyPath}.");
            }

            BackupExisting(privateKeyPath);
            BackupExisting(publicKeyPath);
        }

        var comment = $"my-ssh:{server.Alias}:{Guid.NewGuid():N}";
        var keygen = await _runner.RunAsync(
            _tools.SshKeygenPath,
            ["-t", "ed25519", "-f", privateKeyPath, "-C", comment, "-N", ""],
            TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        if (keygen.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(keygen.StandardError)
                ? "ssh-keygen failed."
                : keygen.StandardError.Trim());
        }

        var publicKey = (await File.ReadAllTextAsync(publicKeyPath, cancellationToken)).Trim();
        var token = CreateToken();
        var tempPort = RandomNumberGenerator.GetInt32(42000, 61000);
        var command = BuildBootstrapCommand(publicKey, token, tempPort);

        return new KeyOperation
        {
            Token = token,
            TemporaryPort = tempPort,
            PrivateKeyPath = privateKeyPath,
            PublicKeyPath = publicKeyPath,
            PublicKey = publicKey,
            BootstrapCommand = command,
            Mode = rotate ? "rotate" : "takeover"
        };
    }

    public string BuildCleanupCommand(KeyOperation operation)
    {
        return BuildReleaseScriptCommand(
            CleanupScriptUrl,
            new Dictionary<string, string>
            {
                ["MYSSH_TOKEN"] = operation.Token
            });
    }

    public string BuildDisablePasswordCommand(KeyOperation operation)
    {
        return BuildReleaseScriptCommand(
            DisablePasswordScriptUrl,
            new Dictionary<string, string>
            {
                ["MYSSH_TOKEN"] = operation.Token
            });
    }

    private static string BuildBootstrapCommand(string publicKey, string token, int tempPort)
    {
        var publicKeyB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicKey));
        return BuildReleaseScriptCommand(
            BootstrapScriptUrl,
            new Dictionary<string, string>
            {
                ["MYSSH_PUBLIC_KEY_B64"] = publicKeyB64,
                ["MYSSH_TOKEN"] = token,
                ["MYSSH_TEMP_PORT"] = tempPort.ToString()
            });
    }

    private static string BuildReleaseScriptCommand(string scriptUrl, IReadOnlyDictionary<string, string> environment)
    {
        var env = string.Join(" ", environment.Select(item => $"{item.Key}='{EscapeShell(item.Value)}'"));
        return $"export {env}; " +
               $"if [ \"$(id -u)\" -eq 0 ]; then curl -fsSL '{scriptUrl}' | bash; " +
               $"else curl -fsSL '{scriptUrl}' | sudo -E bash; fi";
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "server" : safe;
    }

    private static void BackupExisting(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var backup = $"{path}.{DateTimeOffset.Now:yyyyMMddHHmmss}.bak";
        File.Move(path, backup);
    }

    private static string EscapeShell(string value)
    {
        return value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
    }
}
