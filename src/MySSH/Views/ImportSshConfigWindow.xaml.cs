using System.Windows;
using MySSH.ViewModels;

namespace MySSH.Views;

public partial class ImportSshConfigWindow : Window
{
    private readonly IReadOnlyList<SelectableSshConfigEntry> _entries;

    public ImportSshConfigWindow(IReadOnlyList<SelectableSshConfigEntry> entries)
    {
        InitializeComponent();
        _entries = entries;
        DataContext = _entries;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _entries)
        {
            entry.IsSelected = true;
        }
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _entries)
        {
            entry.IsSelected = false;
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
