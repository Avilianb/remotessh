using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using MySSH.Models;
using MySSH.Services;
using MySSH.Views;

namespace MySSH.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ServerStore _serverStore;
    private readonly SshConfigImporter _sshConfigImporter;
    private readonly LocalSshConfigService _localSshConfig;
    private readonly HostKeyService _hostKeyService;
    private readonly SshService _sshService;
    private readonly KeyManagementService _keyManagement;
    private readonly UpdateCheckService _updateCheck;
    private readonly OperationLogger _logger;
    private readonly OpenSshTools _tools;
    private string _selectedSection = "主页";
    private string _searchText = "";
    private string _selectedTag = "全部";
    private ServerProfile? _selectedServer;
    private TerminalSessionViewModel? _selectedTerminal;
    private KeyOperation? _currentKeyOperation;
    private string _statusText = "就绪";
    private string _updateStatus = "";
    private bool _disablePasswordAfterVerify = true;

    public MainViewModel()
    {
        _tools = new OpenSshTools();
        var runner = new ProcessRunner();
        _serverStore = new ServerStore();
        _sshConfigImporter = new SshConfigImporter();
        _localSshConfig = new LocalSshConfigService();
        _hostKeyService = new HostKeyService(_tools, runner);
        _sshService = new SshService(_tools, runner);
        _keyManagement = new KeyManagementService(_tools, runner);
        _updateCheck = new UpdateCheckService();
        _logger = new OperationLogger();

        FilteredServers = CollectionViewSource.GetDefaultView(Servers);
        FilteredServers.Filter = FilterServer;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is string section && Sections.Contains(section))
            {
                SelectedSection = section;
            }
        });
        AddServerCommand = new AsyncRelayCommand(AddServerAsync);
        EditServerCommand = new AsyncRelayCommand(EditServerAsync, () => SelectedServer is not null);
        DeleteServerCommand = new AsyncRelayCommand(DeleteServerAsync, () => SelectedServer is not null);
        ImportSshConfigCommand = new AsyncRelayCommand(ImportSshConfigAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => SelectedServer is not null);
        OpenTerminalCommand = new AsyncRelayCommand(OpenTerminalAsync, () => SelectedServer is not null);
        CloseTerminalCommand = new RelayCommand(CloseTerminal, () => SelectedTerminal is not null);
        PrepareTakeoverCommand = new AsyncRelayCommand(() => PrepareKeyOperationAsync(rotate: false), () => SelectedServer is not null);
        PrepareRotationCommand = new AsyncRelayCommand(() => PrepareKeyOperationAsync(rotate: true), () => SelectedServer is not null);
        StartKeyWorkbenchCommand = new AsyncRelayCommand(StartKeyWorkbenchAsync);
        CopyBootstrapCommand = new RelayCommand(CopyBootstrap, () => CurrentKeyOperation is not null);
        ProbeTemporaryPortCommand = new AsyncRelayCommand(ProbeTemporaryPortAsync, () => SelectedServer is not null && CurrentKeyOperation is not null);
        VerifyTemporaryKeyCommand = new AsyncRelayCommand(VerifyTemporaryKeyAsync, () => SelectedServer is not null && CurrentKeyOperation is not null);
        VerifyOfficialKeyCommand = new AsyncRelayCommand(VerifyOfficialKeyAsync, () => SelectedServer is not null && CurrentKeyOperation is not null);
        CloseTemporaryEntryCommand = new AsyncRelayCommand(CloseTemporaryEntryAsync, () => CanCloseTemporaryEntry);
        DisablePasswordLoginCommand = new AsyncRelayCommand(DisablePasswordLoginAsync, () => CanDisablePasswordLogin);
        CheckAppUpdateCommand = new AsyncRelayCommand(CheckAppUpdateAsync);
        CheckScriptUpdateCommand = new AsyncRelayCommand(CheckScriptUpdateAsync);
    }

    public ObservableCollection<string> Sections { get; } =
    [
        "主页",
        "SSH 连接",
        "服务器信息",
        "服务器密钥",
        "设置"
    ];

    public ObservableCollection<ServerProfile> Servers { get; } = [];

    public ObservableCollection<string> Tags { get; } = ["全部"];

    public ObservableCollection<TerminalSessionViewModel> TerminalSessions { get; } = [];

    public ObservableCollection<string> OperationLog { get; } = [];

    public ICollectionView FilteredServers { get; }

    public string SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilteredServers.Refresh();
            }
        }
    }

    public string SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetProperty(ref _selectedTag, value))
            {
                FilteredServers.Refresh();
            }
        }
    }

    public ServerProfile? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                RaiseServerCommandState();
            }
        }
    }

    public TerminalSessionViewModel? SelectedTerminal
    {
        get => _selectedTerminal;
        set
        {
            if (SetProperty(ref _selectedTerminal, value))
            {
                CloseTerminalCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public KeyOperation? CurrentKeyOperation
    {
        get => _currentKeyOperation;
        private set
        {
            if (SetProperty(ref _currentKeyOperation, value))
            {
                OnPropertyChanged(nameof(HasKeyOperation));
                OnPropertyChanged(nameof(CanCloseTemporaryEntry));
                OnPropertyChanged(nameof(CanDisablePasswordLogin));
                RaiseKeyCommandState();
            }
        }
    }

    public bool HasServers => Servers.Count > 0;

    public bool HasNoServers => Servers.Count == 0;

    public bool HasTerminalSessions => TerminalSessions.Count > 0;

    public bool HasKeyOperation => CurrentKeyOperation is not null;

    public bool DisablePasswordAfterVerify
    {
        get => _disablePasswordAfterVerify;
        set => SetProperty(ref _disablePasswordAfterVerify, value);
    }

    public bool CanCloseTemporaryEntry => CurrentKeyOperation?.OfficialKeyVerified == true;

    public bool CanDisablePasswordLogin => CurrentKeyOperation?.OfficialKeyVerified == true && DisablePasswordAfterVerify;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";

    public string ScriptReleaseTag => KeyManagementService.ScriptReleaseTag;

    public string ScriptDeliveryMode => KeyManagementService.ScriptDeliveryMode;

    public string ServerStorePath => AppPaths.ServerStorePath;

    public string OperationLogPath => AppPaths.OperationLogPath;

    public AsyncRelayCommand LoadCommand { get; }

    public RelayCommand NavigateCommand { get; }

    public AsyncRelayCommand AddServerCommand { get; }

    public AsyncRelayCommand EditServerCommand { get; }

    public AsyncRelayCommand DeleteServerCommand { get; }

    public AsyncRelayCommand ImportSshConfigCommand { get; }

    public AsyncRelayCommand TestConnectionCommand { get; }

    public AsyncRelayCommand OpenTerminalCommand { get; }

    public RelayCommand CloseTerminalCommand { get; }

    public AsyncRelayCommand PrepareTakeoverCommand { get; }

    public AsyncRelayCommand PrepareRotationCommand { get; }

    public AsyncRelayCommand StartKeyWorkbenchCommand { get; }

    public RelayCommand CopyBootstrapCommand { get; }

    public AsyncRelayCommand ProbeTemporaryPortCommand { get; }

    public AsyncRelayCommand VerifyTemporaryKeyCommand { get; }

    public AsyncRelayCommand VerifyOfficialKeyCommand { get; }

    public AsyncRelayCommand CloseTemporaryEntryCommand { get; }

    public AsyncRelayCommand DisablePasswordLoginCommand { get; }

    public AsyncRelayCommand CheckAppUpdateCommand { get; }

    public AsyncRelayCommand CheckScriptUpdateCommand { get; }

    private async Task LoadAsync()
    {
        try
        {
            Servers.Clear();
            foreach (var server in await _serverStore.LoadAsync())
            {
                Servers.Add(server);
            }

            SelectedServer = Servers.FirstOrDefault();
            RefreshTags();
            RefreshStateProperties();
            StatusText = Servers.Count == 0 ? "服务器列表为空" : $"已加载 {Servers.Count} 台服务器";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            MessageBox.Show(ex.Message, "加载服务器失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AddServerAsync()
    {
        await CreateServerFromDialogAsync("服务器信息");
    }

    private async Task EditServerAsync()
    {
        if (SelectedServer is null)
        {
            return;
        }

        var draft = SelectedServer.Clone();
        var dialog = new ServerEditorWindow(draft) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!ValidateServer(draft, SelectedServer.Id))
        {
            return;
        }

        SelectedServer.CopyEditableFieldsFrom(draft);
        await SaveServersAsync();
        FilteredServers.Refresh();
    }

    private async Task DeleteServerAsync()
    {
        if (SelectedServer is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"删除服务器 '{SelectedServer.Alias}'？本地密钥文件不会被删除。",
            "删除服务器",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        Servers.Remove(SelectedServer);
        SelectedServer = Servers.FirstOrDefault();
        await SaveServersAsync();
    }

    private async Task ImportSshConfigAsync()
    {
        var entries = await _sshConfigImporter.ReadDefaultConfigAsync();
        if (entries.Count == 0)
        {
            MessageBox.Show("没有在默认 SSH config 中找到可导入的 Host。", "导入 SSH config", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectable = entries
            .Where(entry => Servers.All(server => !string.Equals(server.Alias, entry.Alias, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => new SelectableSshConfigEntry(entry))
            .ToList();

        if (selectable.Count == 0)
        {
            MessageBox.Show("可导入的 Host 已经全部存在。", "导入 SSH config", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ImportSshConfigWindow(selectable) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var item in selectable.Where(item => item.IsSelected))
        {
            Servers.Add(item.Entry.ToServerProfile());
        }

        await SaveServersAsync();
        SelectedServer = Servers.FirstOrDefault();
        SelectedSection = "服务器信息";
    }

    private async Task TestConnectionAsync()
    {
        if (SelectedServer is null)
        {
            return;
        }

        if (!await EnsureHostTrustedAsync(SelectedServer))
        {
            return;
        }

        StatusText = $"正在测试 {SelectedServer.Alias}...";
        var result = await _sshService.TestConnectionAsync(SelectedServer);
        SelectedServer.LastConnectionStatus = result.Success ? "成功" : result.Message;
        if (result.Success)
        {
            SelectedServer.Machine = result.Snapshot;
            SelectedServer.LastConnectedAtUtc = DateTimeOffset.UtcNow;
        }

        await _logger.AppendAsync(SelectedServer.Alias, "test-connection", result.Message);
        await SaveServersAsync();
        FilteredServers.Refresh();
        StatusText = result.Success ? "连接成功" : result.Message;
    }

    private async Task OpenTerminalAsync()
    {
        if (SelectedServer is null)
        {
            return;
        }

        if (!await EnsureHostTrustedAsync(SelectedServer))
        {
            return;
        }

        var session = new TerminalSessionViewModel(SelectedServer, _tools, _sshService);
        TerminalSessions.Add(session);
        SelectedTerminal = session;
        RefreshStateProperties();
        SelectedSection = "SSH 连接";
        await session.StartAsync();
    }

    private void CloseTerminal()
    {
        if (SelectedTerminal is null)
        {
            return;
        }

        SelectedTerminal.Dispose();
        TerminalSessions.Remove(SelectedTerminal);
        SelectedTerminal = TerminalSessions.FirstOrDefault();
        RefreshStateProperties();
    }

    private async Task PrepareKeyOperationAsync(bool rotate, bool copyAfterPrepare = false)
    {
        if (SelectedServer is null)
        {
            return;
        }

        var overwrite = false;
        try
        {
            CurrentKeyOperation = await _keyManagement.PrepareOperationAsync(SelectedServer, rotate, overwriteExisting: false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Key already exists", StringComparison.OrdinalIgnoreCase))
        {
            var confirm = MessageBox.Show(
                $"{ex.Message}{Environment.NewLine}备份已有 key 后重新生成？",
                "生成本机密钥",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            overwrite = true;
        }

        if (overwrite)
        {
            CurrentKeyOperation = await _keyManagement.PrepareOperationAsync(SelectedServer, rotate, overwriteExisting: true);
        }

        OperationLog.Insert(0, $"{DateTimeOffset.Now:t} 已生成本机 key 和复制命令。");
        await _logger.AppendAsync(SelectedServer.Alias, rotate ? "prepare-rotation" : "prepare-takeover", "Generated local key and bootstrap command.");
        SelectedSection = "服务器密钥";

        if (copyAfterPrepare)
        {
            CopyBootstrap();
        }
    }

    private async Task StartKeyWorkbenchAsync()
    {
        SelectedSection = "服务器密钥";

        if (SelectedServer is null)
        {
            var created = await CreateTakeoverTargetAsync();
            if (!created)
            {
                StatusText = "已取消生成接管脚本。";
                return;
            }
        }

        if (SelectedServer is null)
        {
            return;
        }

        await PrepareKeyOperationAsync(rotate: false, copyAfterPrepare: true);
    }

    private void CopyBootstrap()
    {
        if (CurrentKeyOperation is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(CurrentKeyOperation.BootstrapCommand);
            OperationLog.Insert(0, $"{DateTimeOffset.Now:t} 已复制服务器执行命令。");
            StatusText = "接管脚本命令已复制到剪贴板";
        }
        catch (Exception ex)
        {
            StatusText = $"复制失败：{ex.Message}";
            MessageBox.Show(ex.Message, "复制执行命令失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> CreateServerFromDialogAsync(string sectionAfterSave)
    {
        var draft = new ServerProfile { Port = 22, Tags = ["takeover"] };
        var dialog = new ServerEditorWindow(draft) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        if (!ValidateServer(draft))
        {
            return false;
        }

        draft.CreatedAtUtc = DateTimeOffset.UtcNow;
        draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Servers.Add(draft);
        SelectedServer = draft;
        await SaveServersAsync();
        SelectedSection = sectionAfterSave;
        return true;
    }

    private async Task<bool> CreateTakeoverTargetAsync()
    {
        var dialog = new TakeoverTargetWindow { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var host = NormalizeTakeoverHost(dialog.HostOrDomain);
        if (Servers.Any(server => string.Equals(server.Host, host, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedServer = Servers.First(server => string.Equals(server.Host, host, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        var server = new ServerProfile
        {
            Alias = MakeUniqueAlias(MakeAliasFromHost(host)),
            Host = host,
            Port = 22,
            User = "root",
            Tags = ["takeover"],
            Notes = "Created from first-time key takeover. Login user defaults to root for later verification; edit if the server script installs the key for another user.",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        Servers.Add(server);
        SelectedServer = server;
        await SaveServersAsync();
        SelectedSection = "服务器密钥";
        return true;
    }

    private async Task ProbeTemporaryPortAsync()
    {
        if (SelectedServer is null || CurrentKeyOperation is null)
        {
            return;
        }

        var open = await _sshService.ProbePortAsync(SelectedServer.Host, CurrentKeyOperation.TemporaryPort, TimeSpan.FromSeconds(8));
        CurrentKeyOperation.TemporaryPortOpen = open;
        OnPropertyChanged(nameof(CurrentKeyOperation));
        OperationLog.Insert(0, open
            ? $"{DateTimeOffset.Now:t} 临时端口已开放。"
            : $"{DateTimeOffset.Now:t} 临时端口未探测到开放，请检查云安全组、服务器防火墙和脚本输出。");
        await _logger.AppendAsync(SelectedServer.Alias, "probe-temp-port", open ? "Temporary port open." : "Temporary port closed.");
    }

    private async Task VerifyTemporaryKeyAsync()
    {
        if (SelectedServer is null || CurrentKeyOperation is null)
        {
            return;
        }

        var tempServer = SelectedServer.Clone();
        tempServer.Port = CurrentKeyOperation.TemporaryPort;
        if (!await EnsureHostTrustedAsync(tempServer))
        {
            return;
        }

        var result = await _sshService.TestConnectionAsync(SelectedServer, CurrentKeyOperation.PrivateKeyPath, CurrentKeyOperation.TemporaryPort);
        CurrentKeyOperation.TemporaryKeyVerified = result.Success;
        OnPropertyChanged(nameof(CurrentKeyOperation));
        OperationLog.Insert(0, result.Success
            ? $"{DateTimeOffset.Now:t} 新 key 已通过临时端口验证。"
            : $"{DateTimeOffset.Now:t} 临时端口验证失败：{result.Message}");
        await _logger.AppendAsync(SelectedServer.Alias, "verify-temp-key", result.Message);
    }

    private async Task VerifyOfficialKeyAsync()
    {
        if (SelectedServer is null || CurrentKeyOperation is null)
        {
            return;
        }

        if (!CurrentKeyOperation.TemporaryKeyVerified)
        {
            MessageBox.Show("请先通过临时端口验证新 key。", "验证正式端口", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = await _sshService.TestConnectionAsync(SelectedServer, CurrentKeyOperation.PrivateKeyPath, SelectedServer.Port);
        CurrentKeyOperation.OfficialKeyVerified = result.Success;
        if (result.Success)
        {
            SelectedServer.KeyPath = CurrentKeyOperation.PrivateKeyPath;
            SelectedServer.Machine = result.Snapshot;
            SelectedServer.LastConnectionStatus = "成功";
            SelectedServer.LastConnectedAtUtc = DateTimeOffset.UtcNow;
            await _localSshConfig.UpsertManagedHostAsync(SelectedServer);
            await SaveServersAsync();
        }

        OnPropertyChanged(nameof(CurrentKeyOperation));
        OnPropertyChanged(nameof(CanCloseTemporaryEntry));
        OnPropertyChanged(nameof(CanDisablePasswordLogin));
        RaiseKeyCommandState();
        OperationLog.Insert(0, result.Success
            ? $"{DateTimeOffset.Now:t} 正式端口 + 新 key 验证成功，已更新本机 SSH config。"
            : $"{DateTimeOffset.Now:t} 正式端口验证失败：{result.Message}");
        await _logger.AppendAsync(SelectedServer.Alias, "verify-official-key", result.Message);
    }

    private async Task CloseTemporaryEntryAsync()
    {
        if (SelectedServer is null || CurrentKeyOperation is null || !CanCloseTemporaryEntry)
        {
            return;
        }

        var command = _keyManagement.BuildCleanupCommand(CurrentKeyOperation);
        var result = await _sshService.RunCommandAsync(SelectedServer, command, CurrentKeyOperation.PrivateKeyPath, SelectedServer.Port, TimeSpan.FromSeconds(30));
        CurrentKeyOperation.TemporaryEntryClosed = result.ExitCode == 0;
        OnPropertyChanged(nameof(CurrentKeyOperation));
        OperationLog.Insert(0, result.ExitCode == 0
            ? $"{DateTimeOffset.Now:t} 临时入口已关闭。"
            : $"{DateTimeOffset.Now:t} 关闭临时入口失败：{FirstLine(result.StandardError, result.StandardOutput)}");
        await _logger.AppendAsync(SelectedServer.Alias, "close-temp-entry", result.ExitCode == 0 ? "Temporary entry closed." : FirstLine(result.StandardError, result.StandardOutput));
    }

    private async Task DisablePasswordLoginAsync()
    {
        if (SelectedServer is null || CurrentKeyOperation is null || !CanDisablePasswordLogin)
        {
            return;
        }

        var confirm = MessageBox.Show(
            "确认禁用密码登录？该操作只会在新 key 已验证后执行。",
            "禁用密码登录",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var command = _keyManagement.BuildDisablePasswordCommand(CurrentKeyOperation);
        var result = await _sshService.RunCommandAsync(SelectedServer, command, CurrentKeyOperation.PrivateKeyPath, SelectedServer.Port, TimeSpan.FromSeconds(30));
        OperationLog.Insert(0, result.ExitCode == 0
            ? $"{DateTimeOffset.Now:t} 已请求禁用密码登录。"
            : $"{DateTimeOffset.Now:t} 禁用密码登录失败：{FirstLine(result.StandardError, result.StandardOutput)}");
        await _logger.AppendAsync(SelectedServer.Alias, "disable-password-login", result.ExitCode == 0 ? "Password login disabled." : FirstLine(result.StandardError, result.StandardOutput));
    }

    private async Task CheckAppUpdateAsync()
    {
        UpdateStatus = "正在检查应用更新...";
        UpdateStatus = await _updateCheck.CheckAppAsync();
    }

    private async Task CheckScriptUpdateAsync()
    {
        UpdateStatus = "正在检查脚本 Release...";
        UpdateStatus = await _updateCheck.CheckScriptAsync();
    }

    private async Task<bool> EnsureHostTrustedAsync(ServerProfile server)
    {
        if (_hostKeyService.IsTrusted(server))
        {
            return true;
        }

        var scan = await _hostKeyService.ScanAsync(server);
        if (!scan.Success)
        {
            MessageBox.Show(scan.ErrorMessage, "Host key 扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        var confirm = MessageBox.Show(
            $"首次连接 {server.Endpoint}。{Environment.NewLine}Host key fingerprint: {scan.Fingerprint}{Environment.NewLine}确认信任并写入 known_hosts？",
            "确认 Host Key",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return false;
        }

        await _hostKeyService.TrustAsync(server, scan.KeyLines);
        return true;
    }

    private async Task SaveServersAsync()
    {
        await _serverStore.SaveAsync(Servers);
        RefreshTags();
        RefreshStateProperties();
        FilteredServers.Refresh();
    }

    private bool ValidateServer(ServerProfile server, string? existingId = null)
    {
        if (string.IsNullOrWhiteSpace(server.Alias) ||
            string.IsNullOrWhiteSpace(server.Host) ||
            string.IsNullOrWhiteSpace(server.User) ||
            server.Port is < 1 or > 65535)
        {
            MessageBox.Show("请填写 alias、host、user，并确认端口在 1-65535 之间。", "服务器信息", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var duplicate = Servers.Any(item =>
            item.Id != existingId &&
            string.Equals(item.Alias, server.Alias, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            MessageBox.Show("alias 已存在，请换一个名称。", "服务器信息", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private static string NormalizeTakeoverHost(string value)
    {
        value = value.Trim();
        if (value.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            value = value[6..];
        }
        else if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            value = value[7..];
        }
        else if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = value[8..];
        }

        var slash = value.IndexOfAny(['/', '\\']);
        if (slash >= 0)
        {
            value = value[..slash];
        }

        return value.Trim();
    }

    private string MakeUniqueAlias(string baseAlias)
    {
        var alias = baseAlias;
        var counter = 2;
        while (Servers.Any(server => string.Equals(server.Alias, alias, StringComparison.OrdinalIgnoreCase)))
        {
            alias = $"{baseAlias}-{counter}";
            counter++;
        }

        return alias;
    }

    private static string MakeAliasFromHost(string host)
    {
        var chars = host
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
            .ToArray();
        var alias = new string(chars).Trim('.', '-', '_');
        return string.IsNullOrWhiteSpace(alias) ? "server" : alias;
    }

    private bool FilterServer(object item)
    {
        if (item is not ServerProfile server)
        {
            return false;
        }

        var matchesTag = SelectedTag == "全部" || server.Tags.Any(tag => string.Equals(tag, SelectedTag, StringComparison.OrdinalIgnoreCase));
        if (!matchesTag)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return server.Alias.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || server.Host.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || server.User.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || server.TagsText.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshTags()
    {
        var selected = SelectedTag;
        Tags.Clear();
        Tags.Add("全部");

        foreach (var tag in Servers.SelectMany(server => server.Tags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag))
        {
            Tags.Add(tag);
        }

        SelectedTag = Tags.Contains(selected) ? selected : "全部";
    }

    private void RefreshStateProperties()
    {
        OnPropertyChanged(nameof(HasServers));
        OnPropertyChanged(nameof(HasNoServers));
        OnPropertyChanged(nameof(HasTerminalSessions));
    }

    private void RaiseServerCommandState()
    {
        EditServerCommand.RaiseCanExecuteChanged();
        DeleteServerCommand.RaiseCanExecuteChanged();
        TestConnectionCommand.RaiseCanExecuteChanged();
        OpenTerminalCommand.RaiseCanExecuteChanged();
        PrepareTakeoverCommand.RaiseCanExecuteChanged();
        PrepareRotationCommand.RaiseCanExecuteChanged();
        StartKeyWorkbenchCommand.RaiseCanExecuteChanged();
        RaiseKeyCommandState();
    }

    private void RaiseKeyCommandState()
    {
        CopyBootstrapCommand.RaiseCanExecuteChanged();
        ProbeTemporaryPortCommand.RaiseCanExecuteChanged();
        VerifyTemporaryKeyCommand.RaiseCanExecuteChanged();
        VerifyOfficialKeyCommand.RaiseCanExecuteChanged();
        CloseTemporaryEntryCommand.RaiseCanExecuteChanged();
        DisablePasswordLoginCommand.RaiseCanExecuteChanged();
    }

    private static string FirstLine(params string[] values)
    {
        foreach (var value in values)
        {
            var first = value
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return "未知错误";
    }
}
