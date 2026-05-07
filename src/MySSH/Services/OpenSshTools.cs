namespace MySSH.Services;

public sealed class OpenSshTools
{
    public string SshPath { get; } = Resolve("ssh.exe");

    public string SshKeygenPath { get; } = Resolve("ssh-keygen.exe");

    public string SshKeyscanPath { get; } = Resolve("ssh-keyscan.exe");

    private static string Resolve(string fileName)
    {
        var systemPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "OpenSSH",
            fileName);

        if (File.Exists(systemPath))
        {
            return systemPath;
        }

        return fileName;
    }
}
