using System.Windows;

namespace MySSH.Views;

public partial class TakeoverTargetWindow : Window
{
    public TakeoverTargetWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => HostTextBox.Focus();
    }

    public string HostOrDomain { get; private set; } = "";

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var value = HostTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            ErrorTextBlock.Text = "请输入 IP 或域名。";
            return;
        }

        HostOrDomain = value;
        DialogResult = true;
        Close();
    }
}
