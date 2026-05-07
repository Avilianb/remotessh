using MySSH.Models;

namespace MySSH.ViewModels;

public sealed class SelectableSshConfigEntry : ObservableObject
{
    private bool _isSelected = true;

    public SelectableSshConfigEntry(SshConfigEntry entry)
    {
        Entry = entry;
    }

    public SshConfigEntry Entry { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Alias => Entry.Alias;

    public string HostName => Entry.HostName;

    public int Port => Entry.Port;

    public string User => Entry.User;

    public string IdentityFile => Entry.IdentityFile;
}
