using System.Windows;
using MySSH.ViewModels;

namespace MySSH;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.LoadCommand.Execute(null);
        Closed += (_, _) =>
        {
            foreach (var session in _viewModel.TerminalSessions.ToList())
            {
                session.Dispose();
            }
        };
    }
}
