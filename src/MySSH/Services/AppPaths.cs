namespace MySSH.Services;

public static class AppPaths
{
    public static string AppDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "My SSH");

    public static string ServerStorePath { get; } = Path.Combine(AppDataRoot, "servers.json");

    public static string OperationLogPath { get; } = Path.Combine(AppDataRoot, "operations.log");

    public static string UserProfile { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string SshRoot { get; } = Path.Combine(UserProfile, ".ssh");

    public static string ManagedKeysRoot { get; } = Path.Combine(SshRoot, "my_ssh");

    public static string SshConfigPath { get; } = Path.Combine(SshRoot, "config");

    public static string KnownHostsPath { get; } = Path.Combine(SshRoot, "known_hosts");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(SshRoot);
        Directory.CreateDirectory(ManagedKeysRoot);
    }
}
