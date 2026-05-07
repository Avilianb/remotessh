using System.Windows;
using Microsoft.Win32;
using MySSH.Models;

namespace MySSH.Views;

public partial class ServerEditorWindow : Window
{
    private readonly ServerProfile _server;

    public ServerEditorWindow(ServerProfile server)
    {
        InitializeComponent();
        _server = server;
        DataContext = _server;
    }

    private void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择私钥文件",
            CheckFileExists = true,
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _server.KeyPath = dialog.FileName;
            DataContext = null;
            DataContext = _server;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
