using System.Diagnostics;
using System.Text;
using System.Windows;
using MySSH.Models;
using MySSH.Services;

namespace MySSH.ViewModels;

public sealed class TerminalSessionViewModel : ObservableObject, IDisposable
{
    private readonly OpenSshTools _tools;
    private readonly SshService _sshService;
    private readonly StringBuilder _buffer = new();
    private Process? _process;
    private string _output = "";
    private string _pendingInput = "";
    private bool _isConnected;
    private bool _sessionLoggingEnabled;
    private string _status = "未连接";

    public TerminalSessionViewModel(ServerProfile server, OpenSshTools tools, SshService sshService)
    {
        Server = server.Clone();
        _tools = tools;
        _sshService = sshService;
        Title = string.IsNullOrWhiteSpace(server.Alias) ? server.Endpoint : server.Alias;
        SendInputCommand = new RelayCommand(SendInput, () => IsConnected);
        DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
        ReconnectCommand = new AsyncRelayCommand(StartAsync, () => !IsConnected);
    }

    public ServerProfile Server { get; }

    public string Title { get; }

    public string Output
    {
        get => _output;
        private set => SetProperty(ref _output, value);
    }

    public string PendingInput
    {
        get => _pendingInput;
        set => SetProperty(ref _pendingInput, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                SendInputCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
                ReconnectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool SessionLoggingEnabled
    {
        get => _sessionLoggingEnabled;
        set => SetProperty(ref _sessionLoggingEnabled, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public RelayCommand SendInputCommand { get; }

    public RelayCommand DisconnectCommand { get; }

    public AsyncRelayCommand ReconnectCommand { get; }

    public Task StartAsync()
    {
        Disconnect();

        var startInfo = new ProcessStartInfo(_tools.SshPath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in _sshService.BuildBaseArgs(Server, Server.KeyPath, Server.Port, batchMode: false))
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.ArgumentList.Insert(0, "-tt");

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, args) => AppendLine(args.Data);
        _process.ErrorDataReceived += (_, args) => AppendLine(args.Data);
        _process.Exited += (_, _) =>
        {
            IsConnected = false;
            Status = "已断开";
            AppendLine("[session closed]");
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        IsConnected = true;
        Status = "已连接";
        AppendLine($"[connecting to {Server.User}@{Server.Endpoint}]");
        return Task.CompletedTask;
    }

    public void SendInput()
    {
        if (_process is null || _process.HasExited || string.IsNullOrEmpty(PendingInput))
        {
            return;
        }

        _process.StandardInput.WriteLine(PendingInput);
        AppendLine($"> {PendingInput}");
        PendingInput = "";
    }

    public void Disconnect()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.WriteLine("exit");
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // SSH may already be closing.
        }
        finally
        {
            _process.Dispose();
            _process = null;
            IsConnected = false;
            Status = "已断开";
        }
    }

    public void Dispose()
    {
        Disconnect();
    }

    private void AppendLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            _buffer.AppendLine(line);
            Output = _buffer.ToString();
        });
    }
}
