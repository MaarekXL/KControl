using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using KeryxControl.Infrastructure;
using KeryxControl.Models;
using KeryxControl.Services;

namespace KeryxControl.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly NvidiaService _nvidia = new();
    private readonly MinerService _miner = new();
    private readonly NodeService _node = new();
    private readonly MinerStatsService _statsApi = new();
    private readonly MinerLogParser _logParser = new();
    private readonly IpfsPreflightService _ipfs = new();
    private readonly SettingsService _settingsService = new();
    private readonly NodeSyncTracker _nodeSyncTracker = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _pendingLogGate = new();
    private readonly Queue<(string Line, bool IsMiner)> _pendingLogs = new();
    private readonly Queue<LogEntry> _pausedLogEntries = new();
    private readonly Dictionary<string, PowerChange> _powerChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> _minerGpuIndexMap = [];
    private readonly HashSet<string> _pausedBlockers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeBlockers = new(StringComparer.OrdinalIgnoreCase);
    private AppConfig _config = new();
    private UserSettings _settings = new();
    private bool _logDrainScheduled, _logPaused, _logPausedForError, _shutdownStarted, _minerStopRequested, _nodeStopRequested, _nodeReachable, _shutdownManagedKubo;
    private int _droppedLogCount, _newLogCount, _lastDisplayedSyncPercent = -1, _lastDisplayedModelPercent = -1;
    private string _lastNodeLine = "", _lastMinerLine = "", _language = "fr", _wallet = "", _nodeAddress = "127.0.0.1", _poolAddress = "", _status = "", _statusKey = "StatusInitializing", _statusForeground = "#8BFFAE", _statusDot = "#22E66D";
    private DateTime _lastNodeLineAt = DateTime.MinValue, _lastMinerLineAt = DateTime.MinValue, _lastMonitoringError = DateTime.MinValue;
    private DateTime _lastRelayWarningAt = DateTime.MinValue;
    private int _relayWarningCount;
    private int _nodePort = 22110;
    private long _accepted, _rejected;
    private int? _nodeSyncPercent;
    private bool _nodeSyncKnown;
    private string _hashrate = "0.00 MH/s", _power = "— W", _temperature = "— °C", _utilization = "— %", _memory = "—", _efficiency = "— MH/W", _uptime = "—", _nodeSyncText = "", _modelStatusText = "", _serviceStatusText = "";
    private double _modelProgress;
    private bool _modelProgressVisible;
    private GpuDeviceViewModel? _selectedPowerGpu;
    private MiningModeOptionViewModel? _selectedMiningMode;
    private string _escrowPublicKey = "", _escrowCertificate = "", _escrowStatus = "";
    private int _escrowKeyLinesRemaining;
    private MinerStats _fallbackStats = new();

    public ObservableCollection<GpuDeviceViewModel> Gpus { get; } = [];
    public ObservableCollection<TierOptionViewModel> TierOptions { get; } = [];
    public ObservableCollection<MiningModeOptionViewModel> MiningModeOptions { get; } = [];
    public ObservableCollection<LogEntry> Logs { get; } = [];

    public string Language { get => _language; private set => Set(ref _language, value); }
    public string Wallet { get => _wallet; set { Set(ref _wallet, value); RefreshCommands(); } }
    public string PoolAddress { get => _poolAddress; set { Set(ref _poolAddress, value); RefreshCommands(); } }
    public string NodeAddress { get => _nodeAddress; set { if (Set(ref _nodeAddress, value)) ResetExternalNodeSyncState(); RefreshCommands(); } }
    public int NodePort { get => _nodePort; set { if (Set(ref _nodePort, value)) ResetExternalNodeSyncState(); RefreshCommands(); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string StatusForeground { get => _statusForeground; private set => Set(ref _statusForeground, value); }
    public string StatusDot { get => _statusDot; private set => Set(ref _statusDot, value); }
    public string Hashrate { get => _hashrate; private set => Set(ref _hashrate, value); }
    public string Power { get => _power; private set => Set(ref _power, value); }
    public string Temperature { get => _temperature; private set => Set(ref _temperature, value); }
    public string Utilization { get => _utilization; private set => Set(ref _utilization, value); }
    public string Memory { get => _memory; private set => Set(ref _memory, value); }
    public string Efficiency { get => _efficiency; private set => Set(ref _efficiency, value); }
    public string Uptime { get => _uptime; private set => Set(ref _uptime, value); }
    public long Accepted { get => _accepted; private set => Set(ref _accepted, value); }
    public long Rejected { get => _rejected; private set => Set(ref _rejected, value); }
    public string NodeSyncText { get => _nodeSyncText; private set => Set(ref _nodeSyncText, value); }
    public int? NodeSyncPercent { get => _nodeSyncPercent; private set { Set(ref _nodeSyncPercent, value); RefreshNodeSyncText(); RefreshCommands(); } }
    public bool NodeSyncKnown { get => _nodeSyncKnown; private set { Set(ref _nodeSyncKnown, value); RefreshNodeSyncText(); RefreshCommands(); } }
    public string ModelStatusText { get => _modelStatusText; private set => Set(ref _modelStatusText, value); }
    public string ServiceStatusText { get => _serviceStatusText; private set => Set(ref _serviceStatusText, value); }
    public double ModelProgress { get => _modelProgress; private set => Set(ref _modelProgress, value); }
    public bool ModelProgressVisible { get => _modelProgressVisible; private set => Set(ref _modelProgressVisible, value); }
    public bool IsLogPaused { get => _logPaused; private set { Set(ref _logPaused, value); Raise(nameof(LogPauseText)); } }
    public string LogPauseText => _logPausedForError ? T("LogPausedError") : string.Format(T("LogPausedCount"), NewLogCount);
    public int NewLogCount { get => _newLogCount; private set { Set(ref _newLogCount, value); Raise(nameof(LogPauseText)); } }
    public bool IsMinerRunning => _miner.IsRunning;
    public bool IsNodeRunning => _node.IsOwnedRunning;
    public bool HasActiveProcesses => IsMinerRunning || IsNodeRunning;
    public bool ConfigurationEnabled => !IsMinerRunning;
    public bool ConnectionConfigurationEnabled => !IsMinerRunning && !IsNodeRunning;
    public MiningModeOptionViewModel? SelectedMiningMode
    {
        get => _selectedMiningMode;
        set
        {
            if (!Set(ref _selectedMiningMode, value)) return;
            Raise(nameof(IsSoloMode)); Raise(nameof(IsPoolMode)); Raise(nameof(NodeControlsEnabled));
            RefreshNodeSyncText(); RefreshCommands();
            if (!_miner.IsRunning) SetStatus("StatusReady");
        }
    }
    public bool IsPoolMode => SelectedMiningMode?.Id.Equals("pool", StringComparison.OrdinalIgnoreCase) == true;
    public bool IsSoloMode => !IsPoolMode;
    public bool NodeControlsEnabled => IsSoloMode && !_miner.IsRunning;
    public string EscrowPublicKey { get => _escrowPublicKey; private set => Set(ref _escrowPublicKey, value); }
    public string EscrowCertificate { get => _escrowCertificate; set => Set(ref _escrowCertificate, value); }
    public string EscrowStatus { get => _escrowStatus; private set => Set(ref _escrowStatus, value); }
    public GpuDeviceViewModel? SelectedPowerGpu
    {
        get => _selectedPowerGpu;
        set { if (Set(ref _selectedPowerGpu, value)) { Raise(nameof(PowerLimit)); Raise(nameof(PowerMinimum)); Raise(nameof(PowerMaximum)); RefreshCommands(); } }
    }
    public double PowerLimit
    {
        get => SelectedPowerGpu?.PowerLimit ?? 0;
        set { if (SelectedPowerGpu is null) return; SelectedPowerGpu.PowerLimit = value; Raise(); }
    }
    public double PowerMinimum => SelectedPowerGpu?.Info.PowerMinW ?? 0;
    public double PowerMaximum => SelectedPowerGpu?.Info.PowerMaxW ?? 400;
    public double SavedWindowWidth => _settings.WindowWidth;
    public double SavedWindowHeight => _settings.WindowHeight;
    public double? SavedWindowLeft => _settings.WindowLeft;
    public double? SavedWindowTop => _settings.WindowTop;
    public bool SavedWindowMaximized => _settings.WindowMaximized;

    public AsyncCommand StartCommand { get; }
    public AsyncCommand StopCommand { get; }
    public AsyncCommand StartNodeCommand { get; }
    public AsyncCommand StopNodeCommand { get; }
    public AsyncCommand ApplyPowerCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand ResumeLogCommand { get; }
    public AsyncCommand CopyEscrowKeyCommand { get; }
    public AsyncCommand PasteEscrowCertCommand { get; }
    public AsyncCommand SaveEscrowCertCommand { get; }

    public MainViewModel()
    {
        MiningModeOptions.Add(new("solo", "Solo — nœud keryxd", "Solo — keryxd node", _language));
        MiningModeOptions.Add(new("pool", "Pool — Stratum v3", "Pool — Stratum v3", _language));
        _selectedMiningMode = MiningModeOptions[0];
        StartCommand = new(StartAsync, CanStartMiner);
        StopCommand = new(StopAsync, () => _miner.IsRunning);
        StartNodeCommand = new(StartNodeAsync, () => IsSoloMode && !_node.IsOwnedRunning && !_nodeReachable && !_miner.IsRunning);
        StopNodeCommand = new(StopNodeAsync, () => IsSoloMode && _node.IsOwnedRunning && !_miner.IsRunning);
        ApplyPowerCommand = new(ApplyPowerAsync, () => SelectedPowerGpu?.IsSelected == true);
        RefreshCommand = new(DetectAsync, () => !_miner.IsRunning);
        ResumeLogCommand = new(() => { ResumeLog(); return Task.CompletedTask; });
        CopyEscrowKeyCommand = new(CopyEscrowKeyAsync, () => !string.IsNullOrWhiteSpace(EscrowPublicKey));
        PasteEscrowCertCommand = new(PasteEscrowCertificateAsync);
        SaveEscrowCertCommand = new(SaveEscrowCertificateAsync);
        foreach (var command in new[] { StartCommand, StopCommand, StartNodeCommand, StopNodeCommand, ApplyPowerCommand, RefreshCommand, ResumeLogCommand, CopyEscrowKeyCommand, PasteEscrowCertCommand, SaveEscrowCertCommand })
            command.Failed += ex => AddApplicationLog(ex.Message, LogSeverity.Warning);
        _miner.LineReceived += line => QueueExternalLog(line, true);
        _node.LineReceived += line => QueueExternalLog(line, false);
        _miner.Exited += code =>
        {
            if (Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
                _ = dispatcher.BeginInvoke(async () => await OnMinerExitedAsync(code));
        };
        _node.Exited += code =>
        {
            if (Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
                _ = dispatcher.BeginInvoke(async () => await OnNodeExitedAsync(code));
        };
    }

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync(_lifetime.Token);
        _settings.Gpus ??= new Dictionary<string, GpuPreference>(StringComparer.OrdinalIgnoreCase);
        _settings.Gpus = new Dictionary<string, GpuPreference>(_settings.Gpus, StringComparer.OrdinalIgnoreCase);
        Wallet = _settings.Wallet;
        PoolAddress = _settings.PoolAddress ?? "";
        NodeAddress = string.IsNullOrWhiteSpace(_settings.NodeAddress) ? "127.0.0.1" : _settings.NodeAddress;
        NodePort = _settings.NodePort is > 0 and <= 65535 ? _settings.NodePort : 22110;
        SelectedMiningMode = MiningModeOptions.FirstOrDefault(x => x.Id.Equals(_settings.MiningMode, StringComparison.OrdinalIgnoreCase)) ?? MiningModeOptions[0];
        SetLanguage(_settings.Language);
        if (!string.IsNullOrWhiteSpace(_settingsService.LastLoadWarning))
            AddApplicationLog($"{T("SettingsLoadFailed")}: {_settingsService.LastLoadWarning}", LogSeverity.Warning);
        await LoadConfigAsync();
        await DetectAsync();
        _ = MonitorAsync(_lifetime.Token);
    }

    private async Task LoadConfigAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            if (File.Exists(path)) _config = JsonSerializer.Deserialize<AppConfig>(await File.ReadAllTextAsync(path, _lifetime.Token), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (Exception ex) { AddApplicationLog($"{T("ConfigurationInvalid")}: {ex.Message}", LogSeverity.Warning); }
        NormalizeTiers();
    }

    private void NormalizeTiers()
    {
        _config.Miner ??= new MinerConfig();
        _config.Node ??= new NodeConfig();
        _config.Tiers ??= [];
        if (_config.Tiers.Count == 0) _config.Tiers = DefaultTiers();
        foreach (var tier in _config.Tiers)
        {
            if (string.IsNullOrWhiteSpace(tier.Id)) tier.Id = string.IsNullOrWhiteSpace(tier.Argument) ? "default" : tier.Argument.Trim().TrimStart('-');
            if (string.IsNullOrWhiteSpace(tier.ForceName)) tier.ForceName = tier.Id == "standard" ? "default" : tier.Id;
            if (string.IsNullOrWhiteSpace(tier.NameFr)) tier.NameFr = tier.Name;
            if (string.IsNullOrWhiteSpace(tier.NameEn)) tier.NameEn = tier.Name;
        }
        if (_config.Tiers.All(x => !x.IsAuto)) _config.Tiers.Insert(0, new TierConfig { Id = "auto", NameFr = "Auto", NameEn = "Auto" });
        TierOptions.Clear();
        foreach (var tier in _config.Tiers.OrderByDescending(x => x.IsAuto).ThenBy(x => x.MinVramGb)) TierOptions.Add(new(tier, Language));
    }

    private static List<TierConfig> DefaultTiers() =>
    [
        new() { Id = "auto", NameFr = "Auto", NameEn = "Auto" },
        new() { Id = "very-light", NameFr = "Très léger — 8 Go+", NameEn = "Very Light — 8 GB+", Model = "Qwen3.5-9B-abliterated Q5_K_M", MinVramGb = 8, Argument = "--very-light", ForceName = "very-light" },
        new() { Id = "light", NameFr = "Léger — 12 Go+", NameEn = "Light — 12 GB+", Model = "GLM-4-9B-0414 Q6_K", MinVramGb = 12, Argument = "--light", ForceName = "light" },
        new() { Id = "default", NameFr = "Standard — 16 Go+", NameEn = "Standard — 16 GB+", Model = "Gemma-4-12B-abliterated Q6_K", MinVramGb = 16, ForceName = "default" },
        new() { Id = "high", NameFr = "Élevé — 24 Go+", NameEn = "High — 24 GB+", Model = "Qwen3.6-27B Q4_K_M", MinVramGb = 24, Argument = "--high", ForceName = "high" },
        new() { Id = "very-high", NameFr = "Très élevé — 32 Go+", NameEn = "Very High — 32 GB+", Model = "Kimi-Linear-48B Q4_K_M", MinVramGb = 32, Argument = "--very-high", ForceName = "very-high" }
    ];

    private async Task DetectAsync()
    {
        try
        {
            SetStatus("StatusDetecting");
            var selectedPowerUuid = SelectedPowerGpu?.Uuid;
            var currentPreferences = Gpus.ToDictionary(x => x.Uuid, x => new GpuPreference { Selected = x.IsSelected, TierId = x.SelectedTier?.Id ?? "auto", PowerLimit = x.PowerLimit }, StringComparer.OrdinalIgnoreCase);
            var detected = await _nvidia.DetectAsync(_lifetime.Token);
            Gpus.Clear();
            foreach (var info in detected)
            {
                var gpu = new GpuDeviceViewModel(info);
                gpu.SetLanguage(Language);
                var preference = currentPreferences.GetValueOrDefault(info.Uuid) ?? _settings.Gpus.GetValueOrDefault(info.Uuid);
                gpu.IsSelected = preference?.Selected ?? true;
                gpu.PowerLimit = preference?.PowerLimit is double saved ? Math.Clamp(saved, info.PowerMinW, info.PowerMaxW) : info.PowerLimitW;
                gpu.SelectedTier = TierOptions.FirstOrDefault(x => x.Id.Equals(preference?.TierId ?? "auto", StringComparison.OrdinalIgnoreCase)) ?? TierOptions.FirstOrDefault();
                gpu.PropertyChanged += OnGpuPropertyChanged;
                Gpus.Add(gpu);
            }
            SelectedPowerGpu = Gpus.FirstOrDefault(x => x.Uuid == selectedPowerUuid) ?? Gpus.FirstOrDefault(x => x.IsSelected) ?? Gpus.FirstOrDefault();
            _nodeReachable = IsSoloMode && await IsNodeReachableAsync(NodeAddress, NodePort, _lifetime.Token);
            SetStatus(Gpus.Count == 0 ? "StatusNoGpu" : _nodeReachable ? "StatusNodeOnline" : "StatusReady");
            if (!_nodeReachable && IsSoloMode) { NodeSyncKnown = false; NodeSyncPercent = null; }
        }
        catch (Exception ex)
        {
            SetStatus("StatusNvidiaUnavailable");
            AddApplicationLog(ex.Message, LogSeverity.Warning);
        }
        finally { RefreshCommands(); }
    }

    private void OnGpuPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GpuDeviceViewModel gpu) return;
        if (e.PropertyName == nameof(GpuDeviceViewModel.IsSelected))
        {
            if (!gpu.IsSelected && _powerChanges.ContainsKey(gpu.Uuid)) _ = RestorePowerForGpuAsync(gpu.Uuid, false);
            if (SelectedPowerGpu is null || !SelectedPowerGpu.IsSelected) SelectedPowerGpu = Gpus.FirstOrDefault(x => x.IsSelected) ?? Gpus.FirstOrDefault();
        }
        RefreshCommands();
    }

    private bool CanStartMiner()
    {
        if (IsPoolMode)
            return !_miner.IsRunning && Gpus.Any(x => x.IsSelected) && !string.IsNullOrWhiteSpace(Wallet)
                && TryParsePoolEndpoint(PoolAddress, out _, out _);

        var synchronizationReady = _node.IsOwnedRunning && NodeSyncKnown && NodeSyncPercent is >= 100;
        return !_miner.IsRunning && Gpus.Any(x => x.IsSelected) && !string.IsNullOrWhiteSpace(Wallet)
            && !string.IsNullOrWhiteSpace(NodeAddress) && synchronizationReady;
    }

    private async Task StartAsync()
    {
        try
        {
            var selected = Gpus.Where(x => x.IsSelected).OrderBy(x => x.Index).ToArray();
            if (selected.Length == 0) return;
            if (!Regex.IsMatch(Wallet.Trim(), @"^keryx:[a-z0-9]{20,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                SetStatus("StatusStartFailed");
                AddApplicationLog(T("LogInvalidWallet"), LogSeverity.Warning);
                return;
            }
            MinerConnection connection;
            if (IsPoolMode)
            {
                if (!TryParsePoolEndpoint(PoolAddress, out var poolHost, out var poolPort))
                {
                    SetStatus("StatusStartFailed");
                    AddApplicationLog(T("LogInvalidPool"), LogSeverity.Warning);
                    return;
                }
                connection = MinerConnection.Pool(PoolAddress.Trim());
                if (!await IsNodeReachableAsync(poolHost, poolPort, _lifetime.Token))
                    AddApplicationLog(T("LogPoolNotReachable"), LogSeverity.Warning);
            }
            else
            {
                if (!_node.IsOwnedRunning || !NodeSyncKnown || NodeSyncPercent is not >= 100)
                {
                    SetStatus("StatusNodeSyncRequired");
                    AddApplicationLog(T("LogNodeSyncRequired"), LogSeverity.Warning);
                    return;
                }
                if (!await IsNodeReachableAsync(NodeAddress.Trim(), NodePort, _lifetime.Token))
                {
                    SetStatus("StatusNodeUnavailable");
                    AddApplicationLog(string.Format(T("LogNodeMissing"), NodeAddress, NodePort), LogSeverity.Warning);
                    return;
                }
                try
                {
                    var preparation = await _ipfs.PrepareAsync(_config.Miner, _lifetime.Token);
                    if (preparation.GatewayChanged) AddApplicationLog(string.Format(T("LogIpfsPortChanged"), preparation.OldGatewayPort, preparation.NewGatewayPort));
                    if (preparation.TemporaryDirectoryRemoved) AddApplicationLog(T("LogIpfsTempRemoved"));
                    _shutdownManagedKubo = !preparation.KuboWasRunning;
                }
                catch (Exception ex)
                {
                    SetStatus("StatusIpfsError");
                    AddApplicationLog($"IPFS: {ex.Message}", LogSeverity.Error, "ipfs");
                    return;
                }
                connection = MinerConnection.Solo(NodeAddress.Trim(), NodePort);
            }
            var originalStatsPort = _config.Miner.StatsPort;
            _config.Miner.StatsPort = FindAvailableStatsPort(originalStatsPort);
            if (_config.Miner.StatsPort != originalStatsPort)
                AddApplicationLog(string.Format(T("LogStatsPortChanged"), originalStatsPort, _config.Miner.StatsPort), LogSeverity.Warning);
            _fallbackStats = new(); Accepted = Rejected = 0; Hashrate = "0.00 MH/s";
            foreach (var gpu in Gpus) gpu.ResetMinerStats(true);
            ModelProgress = 0; ModelProgressVisible = false; ModelStatusText = ""; _lastDisplayedModelPercent = -1;
            _pausedBlockers.Clear(); _activeBlockers.Clear();
            var launch = Gpus.OrderBy(gpu => gpu.Index).Select(gpu =>
            {
                var tier = gpu.SelectedTier ?? TierOptions.First();
                var automatic = GetAutomaticTier(gpu.Info);
                return new GpuLaunchSelection(gpu.Index, gpu.Uuid, gpu.IsSelected, tier.IsAuto, tier.Config.ForceName, automatic.ForceName);
            }).ToArray();
            _minerGpuIndexMap.Clear();
            foreach (var entry in launch.Where(x => x.IsSelected).OrderBy(x => x.Index).Select((gpu, logicalIndex) => (gpu.Index, logicalIndex)))
                _minerGpuIndexMap[entry.logicalIndex] = entry.Index;
            _minerStopRequested = false;
            await _miner.StartAsync(_config.Miner, Wallet.Trim(), connection, launch, _lifetime.Token);
            SetStatus("StatusMiningStarting");
            AddApplicationLog(T(IsPoolMode ? "LogPoolMinerStarted" : "LogMinerStarted"));
        }
        catch (Exception ex)
        {
            SetStatus("StatusStartFailed");
            AddApplicationLog(ex.Message, LogSeverity.Warning);
        }
        finally { RaiseProcessState(); }
    }

    private TierConfig GetAutomaticTier(GpuInfo gpu)
    {
        var gb = gpu.MemoryTotalMiB / 1024d;
        return _config.Tiers.Where(x => !x.IsAuto && x.MinVramGb <= gb + 0.25).OrderByDescending(x => x.MinVramGb).FirstOrDefault()
            ?? _config.Tiers.First(x => !x.IsAuto);
    }

    private static async Task<bool> IsNodeReachableAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, timeout.Token);
            return true;
        }
        catch { return false; }
    }

    internal static bool TryParsePoolEndpoint(string? value, out string host, out int port)
    {
        host = ""; port = 0;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals("stratum+tcp", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || uri.Port is <= 0 or > 65535)
            return false;
        host = uri.Host; port = uri.Port;
        return true;
    }

    private static int FindAvailableStatsPort(int preferredPort)
    {
        foreach (var port in Enumerable.Range(Math.Clamp(preferredPort, 1024, 65525), 11))
            if (IsLocalTcpPortAvailable(port)) return port;
        throw new InvalidOperationException("No free miner statistics port was found.");
    }

    private static bool IsLocalTcpPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Server.ExclusiveAddressUse = true;
            listener.Start();
            return true;
        }
        catch (SocketException) { return false; }
        finally { try { listener?.Stop(); } catch { } }
    }

    private async Task StopAsync()
    {
        try
        {
            SetStatus("StatusStopping");
            _minerStopRequested = true;
            await _miner.StopAsync(_config.Miner.StopTimeoutSeconds, _lifetime.Token);
            await ShutdownManagedKuboAsync();
            await RestoreAllPowerLimitsAsync(true);
            SetStatus("StatusStopped");
            AddApplicationLog(T("LogMinerStopped"));
        }
        catch (Exception ex) { AddApplicationLog(ex.Message, LogSeverity.Warning); }
        finally { RaiseProcessState(); }
    }

    private async Task StartNodeAsync()
    {
        try
        {
            if (!IsLoopbackHost(NodeAddress))
            {
                SetStatus("StatusStartFailed");
                AddApplicationLog(T("LogNodeLoopbackOnly"), LogSeverity.Warning);
                return;
            }
            _nodeReachable = await IsNodeReachableAsync(NodeAddress.Trim(), NodePort, _lifetime.Token);
            if (_nodeReachable)
            {
                SetStatus("StatusNodeOnline");
                AddApplicationLog(T("LogNodeAlreadyRunning"), LogSeverity.Warning);
                return;
            }
            SetStatus("StatusStartingNode"); NodeSyncKnown = false; NodeSyncPercent = null;
            _nodeSyncTracker.Reset(DateTime.UtcNow);
            _nodeStopRequested = false;
            await _node.StartAsync(_config.Node, NodeAddress.Trim(), NodePort, _lifetime.Token);
            AddApplicationLog(T("LogNodeStarted"));
            await Task.Delay(1200, _lifetime.Token);
            _nodeReachable = await IsNodeReachableAsync(NodeAddress, NodePort, _lifetime.Token);
            SetStatus(_nodeReachable ? "StatusNodeOnline" : "StatusStartingNode");
        }
        catch (FileNotFoundException) { SetStatus("StatusNodeUnavailable"); AddApplicationLog(T("LogNodeMissingExe"), LogSeverity.Warning); }
        catch (Exception ex) { SetStatus("StatusStartFailed"); AddApplicationLog(ex.Message, LogSeverity.Warning); }
        finally { RaiseProcessState(); }
    }

    private async Task StopNodeAsync()
    {
        try
        {
            _nodeStopRequested = true;
            await _node.StopAsync(_lifetime.Token);
            _nodeReachable = false;
            _nodeSyncTracker.Reset(DateTime.UtcNow);
            NodeSyncKnown = false; NodeSyncPercent = null;
            SetStatus("StatusNodeUnavailable");
            AddApplicationLog(T("LogNodeStopped"));
        }
        catch (Exception ex) { AddApplicationLog(ex.Message, LogSeverity.Warning); }
        finally { RaiseProcessState(); }
    }

    private static bool IsLoopbackHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var host = value.Trim();
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private async Task ShutdownManagedKuboAsync()
    {
        if (!_shutdownManagedKubo) return;
        try { await _ipfs.ShutdownAsync(_config.Miner, CancellationToken.None); }
        catch (Exception ex) { AddApplicationLog($"IPFS shutdown: {ex.Message}", LogSeverity.Warning); }
        finally { _shutdownManagedKubo = false; }
    }

    private async Task ApplyPowerAsync()
    {
        var gpu = SelectedPowerGpu;
        if (gpu is null) return;
        try
        {
            var desired = Math.Clamp(gpu.PowerLimit, gpu.Info.PowerMinW, gpu.Info.PowerMaxW);
            if (!_powerChanges.TryGetValue(gpu.Uuid, out var change)) change = new(gpu.Info.PowerLimitW, desired, gpu.Index);
            await _nvidia.SetPowerLimitAsync(gpu.Info, desired, _lifetime.Token);
            if (Math.Abs(desired - change.InitialWatts) < 0.75) _powerChanges.Remove(gpu.Uuid);
            else _powerChanges[gpu.Uuid] = change with { AppliedWatts = desired };
            AddApplicationLog(string.Format(T("LogPowerApplied"), gpu.Index, desired));
        }
        catch (Exception ex) { AddApplicationLog($"Power limit: {ex.Message}", LogSeverity.Warning); }
    }

    private async Task RestoreAllPowerLimitsAsync(bool log)
    {
        foreach (var uuid in _powerChanges.Keys.ToArray()) await RestorePowerForGpuAsync(uuid, log);
    }

    private async Task RestorePowerForGpuAsync(string uuid, bool log)
    {
        if (!_powerChanges.TryGetValue(uuid, out var change)) return;
        try
        {
            var current = (await _nvidia.DetectAsync(CancellationToken.None)).FirstOrDefault(x => x.Uuid.Equals(uuid, StringComparison.OrdinalIgnoreCase));
            if (current is null) throw new InvalidOperationException($"GPU {change.GpuIndex} is no longer available.");
            if (Math.Abs(current.PowerLimitW - change.AppliedWatts) <= 1.0)
            {
                await _nvidia.SetPowerLimitAsync(current, change.InitialWatts, CancellationToken.None);
                var vm = Gpus.FirstOrDefault(x => x.Uuid.Equals(uuid, StringComparison.OrdinalIgnoreCase));
                if (vm is not null) { vm.PowerLimit = change.InitialWatts; Raise(nameof(PowerLimit)); }
                if (log) AddApplicationLog(string.Format(T("LogPowerRestored"), change.GpuIndex, change.InitialWatts));
            }
            _powerChanges.Remove(uuid);
        }
        catch (Exception ex) { if (log) AddApplicationLog($"Power restore: {ex.Message}", LogSeverity.Warning); }
    }

    private async Task MonitorAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                IReadOnlyDictionary<int, GpuMetrics>? metrics = null;
                MinerApiStats? api = null;
                bool? nodeReachable = null;
                try { metrics = await _nvidia.GetAllMetricsAsync(ct); }
                catch (Exception ex)
                {
                    if (DateTime.UtcNow - _lastMonitoringError > TimeSpan.FromMinutes(1))
                    {
                        _lastMonitoringError = DateTime.UtcNow;
                        if (Application.Current?.Dispatcher is { HasShutdownStarted: false } logDispatcher)
                            _ = logDispatcher.BeginInvoke(() => AddApplicationLog($"NVIDIA: {ex.Message}", LogSeverity.Warning));
                    }
                }
                if (_miner.IsRunning) api = await _statsApi.TryGetAsync(_config.Miner.StatsAddress, _config.Miner.StatsPort, ct);
                if (IsSoloMode) nodeReachable = await IsNodeReachableAsync(NodeAddress, NodePort, ct);
                if (Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
                    await dispatcher.InvokeAsync(() => ApplyMonitoring(metrics, api, nodeReachable));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
                await dispatcher.InvokeAsync(() => AddApplicationLog($"Monitoring: {ex.Message}", LogSeverity.Warning));
        }
    }

    private void ApplyMonitoring(IReadOnlyDictionary<int, GpuMetrics>? metrics, MinerApiStats? api, bool? nodeReachable)
    {
        if (nodeReachable is bool reachable)
        {
            var reachabilityChanged = _nodeReachable != reachable;
            _nodeReachable = reachable;
            if (!reachable && !_node.IsOwnedRunning)
            {
                NodeSyncKnown = false;
                NodeSyncPercent = null;
            }
            else if (_node.IsOwnedRunning)
            {
                ApplyNodeSyncSnapshot(_nodeSyncTracker.Tick(DateTime.UtcNow));
            }
            if (reachabilityChanged) RefreshCommands();
        }

        if (metrics is not null)
            foreach (var gpu in Gpus)
                if (metrics.TryGetValue(gpu.Index, out var value)) gpu.ApplyNvidiaMetrics(value);

        if (api is not null)
        {
            Hashrate = $"{api.TotalHashrateHs / 1_000_000d:0.00} MH/s";
            Accepted = IsPoolMode ? _fallbackStats.Accepted : api.AcceptedBlocks;
            Rejected = IsPoolMode ? _fallbackStats.Rejected : api.RejectedBlocks;
            Uptime = FormatUptime(api.UptimeSeconds);
            if (IsSoloMode)
            {
                NodeSyncKnown = true;
                NodeSyncPercent = api.Synced ? 100 : NodeSyncPercent is < 100 ? NodeSyncPercent : 99;
            }
            ServiceStatusText = api.OpoiChallengeActive
                ? T("OpoiInferenceActive")
                : string.IsNullOrWhiteSpace(api.ServiceStatus) || api.ServiceStatus.Equals("clear", StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : string.Format(T("ServiceStanding"), api.ServiceStatus);
            foreach (var device in api.Devices)
            {
                if (TryParseMinerDeviceIndex(device.Id, out var logicalIndex))
                {
                    var physicalIndex = _minerGpuIndexMap.GetValueOrDefault(logicalIndex, logicalIndex);
                    Gpus.FirstOrDefault(x => x.Index == physicalIndex)?.ApplyMinerStats(device);
                }
            }
            if (_miner.IsRunning && (IsPoolMode || api.Synced))
            {
                _activeBlockers.Clear();
                SetStatus("StatusMining");
            }
            else if (_miner.IsRunning && IsSoloMode && !api.Synced && _activeBlockers.Count == 0)
            {
                SetStatus("StatusNodeSynchronizing");
            }
        }
        else if (_miner.IsRunning)
        {
            Hashrate = $"{_fallbackStats.HashrateMh:0.00} MH/s";
            Accepted = _fallbackStats.Accepted;
            Rejected = _fallbackStats.Rejected;
        }

        var selected = Gpus.Where(x => x.IsSelected).ToArray();
        var selectedMetrics = metrics?.Values.Where(x => selected.Any(g => g.Index == x.Index)).ToArray() ?? [];
        var totalPower = selectedMetrics.Sum(x => x.PowerW);
        Power = selectedMetrics.Length > 0 ? $"{totalPower:0.0} W" : "— W";
        Temperature = selectedMetrics.Length > 0 ? $"{selectedMetrics.Max(x => x.TemperatureC):0} °C" : "— °C";
        Utilization = selectedMetrics.Length > 0 ? $"{selectedMetrics.Average(x => x.UtilizationPercent):0} %" : "— %";
        Memory = selectedMetrics.Length > 0 ? $"{selectedMetrics.Sum(x => x.MemoryUsedMiB)} / {selectedMetrics.Sum(x => x.MemoryTotalMiB)} MiB" : "—";
        var hashMh = api?.TotalHashrateHs / 1_000_000d ?? _fallbackStats.HashrateMh;
        Efficiency = totalPower > 0 ? $"{hashMh / totalPower:0.000} MH/W" : "— MH/W";
    }

    private static string FormatUptime(long seconds)
    {
        var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return value.TotalHours >= 1 ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private async Task CopyEscrowKeyAsync()
    {
        try { if (!string.IsNullOrWhiteSpace(EscrowPublicKey)) Clipboard.SetText(EscrowPublicKey); }
        catch (Exception ex) { EscrowStatus = ex.Message; }
        await Task.CompletedTask;
    }

    private async Task PasteEscrowCertificateAsync()
    {
        try { if (Clipboard.ContainsText()) EscrowCertificate = Clipboard.GetText(); }
        catch (Exception ex) { EscrowStatus = ex.Message; }
        await Task.CompletedTask;
    }

    private async Task SaveEscrowCertificateAsync()
    {
        var match = Regex.Match(EscrowCertificate, @"(?i)(?<![0-9a-f])[0-9a-f]{128}(?![0-9a-f])");
        if (!match.Success) { EscrowStatus = T("EscrowInvalid"); return; }
        try
        {
            var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_config.Miner.Executable), AppContext.BaseDirectory);
            var folder = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "escrow.cert"), match.Value.ToLowerInvariant());
            EscrowCertificate = match.Value.ToLowerInvariant();
            EscrowStatus = T(_miner.IsRunning ? "EscrowSavedRestart" : "EscrowSaved");
            _activeBlockers.Remove("cert");
            UpdateBlockingStatus();
            AddApplicationLog(T(_miner.IsRunning ? "EscrowSavedRestartLog" : "EscrowSavedLog"));
        }
        catch (Exception ex) { EscrowStatus = ex.Message; }
    }

    private void QueueExternalLog(string line, bool isMiner)
    {
        var schedule = false;
        lock (_pendingLogGate)
        {
            if (!isMiner && line == _lastNodeLine && DateTime.UtcNow - _lastNodeLineAt < TimeSpan.FromSeconds(1)) return;
            if (!isMiner) { _lastNodeLine = line; _lastNodeLineAt = DateTime.UtcNow; }
            if (isMiner && !IsPoolShareLine(line) && line == _lastMinerLine && DateTime.UtcNow - _lastMinerLineAt < TimeSpan.FromSeconds(1)) return;
            if (isMiner) { _lastMinerLine = line; _lastMinerLineAt = DateTime.UtcNow; }
            if (_pendingLogs.Count >= 2_000) { _pendingLogs.Dequeue(); _droppedLogCount++; }
            _pendingLogs.Enqueue((line, isMiner));
            if (!_logDrainScheduled) { _logDrainScheduled = true; schedule = true; }
        }
        if (schedule && Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DrainPendingLogs));
    }

    private void DrainPendingLogs()
    {
        var batch = new List<(string Line, bool IsMiner)>(250);
        var dropped = 0; var more = false;
        lock (_pendingLogGate)
        {
            while (batch.Count < 250 && _pendingLogs.Count > 0) batch.Add(_pendingLogs.Dequeue());
            dropped = _droppedLogCount; _droppedLogCount = 0;
            more = _pendingLogs.Count > 0;
            if (!more) _logDrainScheduled = false;
        }
        foreach (var item in batch) AddLogCore(item.Line, item.IsMiner);
        if (dropped > 0) AddApplicationLog(string.Format(T("LogLinesSkipped"), dropped), LogSeverity.Warning);
        if (more && Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DrainPendingLogs));
    }

    private void AddApplicationLog(string line, LogSeverity severity = LogSeverity.Info, string? blocker = null) => AddLogEntry(line, severity, blocker);

    private void AddLogCore(string line, bool isMiner)
    {
        if (IsSoloMode) CaptureEscrowKey(line);
        var syncProgressChanged = !isMiner && ParseNodeProgress(line);
        var modelProgressChanged = false;
        var poolStatsChanged = false;
        if (isMiner)
        {
            var previous = _fallbackStats;
            _fallbackStats = _logParser.Parse(line, _fallbackStats, IsPoolMode);
            poolStatsChanged = previous.Accepted != _fallbackStats.Accepted || previous.Rejected != _fallbackStats.Rejected;
            if (IsPoolMode && poolStatsChanged)
            {
                Accepted = _fallbackStats.Accepted;
                Rejected = _fallbackStats.Rejected;
            }
            if (_miner.IsRunning && _fallbackStats.HashrateMh > 0 && Hashrate == "0.00 MH/s") Hashrate = $"{_fallbackStats.HashrateMh:0.00} MH/s";
            modelProgressChanged = ParseModelProgress(line);
        }
        var blocker = IsSoloMode ? DetectBlocker(line) : null;
        var severity = blocker is not null ? LogSeverity.Error : IsNonBlockingProblem(line) ? LogSeverity.Warning : LogSeverity.Info;
        if (ShouldSuppressRoutineLine(line, isMiner, severity, syncProgressChanged, modelProgressChanged, poolStatsChanged)) return;
        if (severity == LogSeverity.Warning && IsRepeatedRelayWarning(line))
        {
            var summarized = SummarizeRelayWarning(line);
            if (summarized is null) return;
            line = summarized;
        }
        AddLogEntry(line, severity, blocker);
    }

    private void AddLogEntry(string line, LogSeverity severity, string? blocker)
    {
        if (blocker is not null)
        {
            _activeBlockers.Add(blocker);
            UpdateBlockingStatus();
        }

        var entry = new LogEntry(DateTime.Now, line, severity);
        if (IsLogPaused)
        {
            if (_pausedLogEntries.Count >= 1_000) _pausedLogEntries.Dequeue();
            _pausedLogEntries.Enqueue(entry);
            NewLogCount = _pausedLogEntries.Count;
            if (blocker is not null && _pausedBlockers.Add(blocker)) PauseLog(true);
            return;
        }

        AppendVisibleLog(entry);
        if (blocker is not null && _pausedBlockers.Add(blocker)) PauseLog(true);
    }

    private void AppendVisibleLog(LogEntry entry)
    {
        Logs.Add(entry);
        while (Logs.Count > 500) Logs.RemoveAt(0);
    }

    private static bool IsNonBlockingProblem(string line) => Regex.IsMatch(line, @"(?i)(?:\[WARN\]|\bWARN\b|\bWARNING\b|\[ERROR\]|\bERROR\b|cannot start)");

    private static bool ShouldSuppressRoutineLine(string line, bool isMiner, LogSeverity severity, bool syncProgressChanged, bool modelProgressChanged, bool poolStatsChanged)
    {
        if (severity != LogSeverity.Info) return false;
        if (isMiner)
        {
            if (Regex.IsMatch(line, @"(?i)\b(?:Current hashrate is|Device #\d+ .*?hash(?:rate)?)\b")) return true;
            if (Regex.IsMatch(line, @"(?i)\d+(?:[.,]\d+)?/\d+(?:[.,]\d+)?\s*(?:MB|GB)\s*\(\d{1,3}%\)")) return !modelProgressChanged;
            if (line.Contains("Shares:", StringComparison.OrdinalIgnoreCase) && line.Contains("Pending:", StringComparison.OrdinalIgnoreCase)) return !poolStatsChanged;
            return false;
        }
        if (line.Contains("IBD:", StringComparison.OrdinalIgnoreCase)) return !syncProgressChanged;
        return Regex.IsMatch(line, @"(?i)\b(?:Accepted (?:\d+ blocks?|block [0-9a-f]{64})|Orphaned \d+ blocks?|Unorphaned (?:\d+ )?blocks?|Processed \d+ blocks?|Received \d+ UTXO set chunks so far|Virtual-index cache|Tx throughput stats|P2P health|Connection manager|Registering p2p flows|P2P Connected|Querying DNS seeder|Retrieved \d+ addresses)\b");
    }

    private static bool IsRepeatedRelayWarning(string line) =>
        line.Contains("HandleRelayBlockRequests flow error", StringComparison.OrdinalIgnoreCase)
        || line.Contains("HandleRelayInvsFlow flow error", StringComparison.OrdinalIgnoreCase);

    private string? SummarizeRelayWarning(string line)
    {
        var now = DateTime.UtcNow;
        _relayWarningCount++;
        if (_relayWarningCount == 1)
        {
            _lastRelayWarningAt = now;
            return line;
        }
        if (now - _lastRelayWarningAt < TimeSpan.FromSeconds(30)) return null;
        var count = _relayWarningCount;
        _relayWarningCount = 0;
        _lastRelayWarningAt = now;
        return string.Format(T("LogRepeatedRelayWarning"), count);
    }

    private static bool TryParseMinerDeviceIndex(string? deviceId, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(deviceId)) return false;
        // Current releases use values such as "#0 NVIDIA ...". Accept the
        // compact "GPU0" spelling as well because it appears in upstream
        // tests and keeps the frontend tolerant of future stats formatting.
        var match = Regex.Match(deviceId, @"(?i)(?:^#\s*|^GPU\s*)(\d+)\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out index);
    }
    private static bool IsPoolShareLine(string line) => Regex.IsMatch(line, @"(?i)\b(?:Share accepted|Share rejected by pool|Stale share|Duplicate share|Low difficulty share|Shares:)\b");
    private static string? DetectBlocker(string line)
    {
        if (line.Contains("Cert does not match this payout address and escrow key", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Cannot read escrow delegation cert", StringComparison.OrdinalIgnoreCase)
            || line.Contains("escrow delegation cert", StringComparison.OrdinalIgnoreCase) && line.Contains("not found", StringComparison.OrdinalIgnoreCase)) return "cert";
        if (line.Contains("IPFS daemon exited before the API was ready", StringComparison.OrdinalIgnoreCase)
            || line.Contains("IPFS API did not become ready", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Failed to start IPFS", StringComparison.OrdinalIgnoreCase)
            || line.Contains("IPFS init failed", StringComparison.OrdinalIgnoreCase)
            || line.Contains("serveHTTPGateway", StringComparison.OrdinalIgnoreCase) && line.Contains("failed", StringComparison.OrdinalIgnoreCase)) return "ipfs";
        return null;
    }

    private bool ParseNodeProgress(string line)
    {
        var snapshot = _nodeSyncTracker.Observe(line, DateTime.UtcNow);
        ApplyNodeSyncSnapshot(snapshot);
        return snapshot.Changed;
    }

    private void ApplyNodeSyncSnapshot(NodeSyncSnapshot snapshot)
    {
        if (!snapshot.Known) return;
        NodeSyncKnown = true;
        NodeSyncPercent = snapshot.Percent;
        _lastDisplayedSyncPercent = snapshot.Percent ?? -1;
        if (!_miner.IsRunning)
            SetStatus(snapshot.Synchronized ? "StatusNodeSynchronized" : "StatusNodeSynchronizing");
    }

    private bool ParseModelProgress(string line)
    {
        var match = Regex.Match(line, @"(?i)([0-9]+(?:[.,][0-9]+)?)/([0-9]+(?:[.,][0-9]+)?)\s*(?:MB|GB)\s*\((\d{1,3})%\)");
        if (match.Success && int.TryParse(match.Groups[3].Value, out var percent))
        {
            percent = Math.Clamp(percent, 0, 100);
            var changed = percent != _lastDisplayedModelPercent;
            _lastDisplayedModelPercent = percent;
            ModelProgress = percent; ModelProgressVisible = percent < 100;
            ModelStatusText = string.Format(T("ModelDownloading"), percent);
            return changed;
        }
        else if (line.Contains("model loaded", StringComparison.OrdinalIgnoreCase) || line.Contains("solver armed", StringComparison.OrdinalIgnoreCase))
        {
            var changed = _lastDisplayedModelPercent != 100;
            _lastDisplayedModelPercent = 100;
            ModelProgress = 100; ModelProgressVisible = false; ModelStatusText = T("ModelReady");
            return changed;
        }
        return false;
    }

    private void CaptureEscrowKey(string line)
    {
        var labelled = Regex.Match(line, @"(?i)(?:escrow pubkey\s*:|escrow key to authorise in your wallet\s*:|paste this escrow key\s*:?)\s*([0-9a-f]{64})");
        if (line.Contains("paste this escrow key", StringComparison.OrdinalIgnoreCase)) _escrowKeyLinesRemaining = 3;
        var standalone = _escrowKeyLinesRemaining > 0
            ? Regex.Match(line, @"(?i)^\s*(?:\[[^\]]+\]\s*)?([0-9a-f]{64})\s*$")
            : Match.Empty;
        var value = labelled.Success ? labelled.Groups[1].Value : standalone.Success ? standalone.Groups[1].Value : "";
        if (_escrowKeyLinesRemaining > 0) _escrowKeyLinesRemaining--;
        if (value.Length != 64) return;
        _escrowKeyLinesRemaining = 0;
        EscrowPublicKey = value.ToLowerInvariant(); EscrowStatus = T("EscrowKeyDetected"); CopyEscrowKeyCommand.Raise();
    }

    public void PauseLog(bool dueToError = false)
    {
        _logPausedForError |= dueToError;
        IsLogPaused = true;
        Raise(nameof(LogPauseText));
    }

    public void ResumeLog()
    {
        _logPausedForError = false;
        IsLogPaused = false;
        while (_pausedLogEntries.Count > 0) AppendVisibleLog(_pausedLogEntries.Dequeue());
        NewLogCount = 0;
        Raise(nameof(LogPauseText));
    }

    public string GetLogText()
    {
        var lines = Logs.Select(x => x.DisplayText).Concat(_pausedLogEntries.Select(x => x.DisplayText));
        return string.Join(Environment.NewLine, lines);
    }

    public void SetLanguage(string? language)
    {
        var normalized = language?.Equals("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "fr";
        Language = normalized;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(x => x.Source?.OriginalString.Contains("Strings.", StringComparison.OrdinalIgnoreCase) == true);
        if (current is not null) dictionaries.Remove(current);
        dictionaries.Add(new ResourceDictionary { Source = new Uri($"Resources/Strings.{normalized}.xaml", UriKind.Relative) });
        foreach (var tier in TierOptions) tier.SetLanguage(normalized);
        foreach (var mode in MiningModeOptions) mode.SetLanguage(normalized);
        foreach (var gpu in Gpus) gpu.SetLanguage(normalized);
        Status = T(_statusKey); RefreshNodeSyncText(); Raise(nameof(LogPauseText));
        // The monitoring loop will rebuild this localized status on its next tick.
        if (!string.IsNullOrWhiteSpace(ServiceStatusText)) ServiceStatusText = "";
        if (string.IsNullOrWhiteSpace(EscrowStatus)) EscrowStatus = T("EscrowWaiting");
    }

    private void RefreshNodeSyncText()
    {
        NodeSyncText = IsPoolMode
            ? T("PoolNoLocalServices")
            : !NodeSyncKnown ? T("NodeSyncUnknown") : NodeSyncPercent is null ? T("NodeNotSynchronized") : NodeSyncPercent is >= 100 ? T("NodeSynchronized") : string.Format(T("NodeSynchronizingPercent"), NodeSyncPercent);
    }

    private static string T(string key) => Application.Current.TryFindResource(key) as string ?? key;
    private void SetStatus(string key)
    {
        _statusKey = key;
        Status = T(key);
        if (key is "StatusAuthorizationRequired" or "StatusIpfsError" or "StatusStartFailed")
        {
            StatusForeground = "#FF8C92";
            StatusDot = "#FF6B73";
        }
        else if (key is "StatusNodeUnavailable" or "StatusNvidiaUnavailable" or "StatusNoGpu" or "StatusNodeSyncRequired" or "StatusNodeSynchronizing")
        {
            StatusForeground = "#F2C66D";
            StatusDot = "#F2B84B";
        }
        else
        {
            StatusForeground = "#8BFFAE";
            StatusDot = "#22E66D";
        }
    }

    private void UpdateBlockingStatus()
    {
        if (_activeBlockers.Contains("cert")) SetStatus("StatusAuthorizationRequired");
        else if (_activeBlockers.Contains("ipfs")) SetStatus("StatusIpfsError");
        else if (_miner.IsRunning) SetStatus("StatusMining");
    }

    private async Task OnMinerExitedAsync(int code)
    {
        Hashrate = "0.00 MH/s";
        Uptime = "—";
        ServiceStatusText = "";
        foreach (var gpu in Gpus) gpu.ResetMinerStats(false);
        if (!_minerStopRequested)
        {
            await ShutdownManagedKuboAsync();
            await RestorePowerAfterUnexpectedExitAsync();
        }
        if (_activeBlockers.Contains("ipfs")) await TryRepairIpfsAfterFailureAsync();
        if (_activeBlockers.Count == 0)
        {
            SetStatus(code == 0 ? "StatusStopped" : "StatusStartFailed");
            if (code != 0) Status = $"{Status} (code {code})";
        }
        else UpdateBlockingStatus();
        RaiseProcessState();
    }

    private async Task TryRepairIpfsAfterFailureAsync()
    {
        try
        {
            await Task.Delay(700);
            if (await _ipfs.RepairTemporaryDirectoryAsync(_config.Miner, CancellationToken.None))
            {
                AddApplicationLog(T("LogIpfsTempRemoved"));
                _activeBlockers.Remove("ipfs");
            }
        }
        catch (Exception ex)
        {
            AddApplicationLog($"IPFS: {ex.Message}", LogSeverity.Error);
        }
    }
    private async Task RestorePowerAfterUnexpectedExitAsync()
    {
        try { await RestoreAllPowerLimitsAsync(true); }
        catch { }
    }
    private async Task OnNodeExitedAsync(int code)
    {
        _nodeReachable = false;
        _nodeSyncTracker.Reset(DateTime.UtcNow);
        if (!_nodeStopRequested && !_shutdownStarted)
        {
            NodeSyncKnown = false;
            NodeSyncPercent = null;
            SetStatus("StatusNodeUnavailable");
            AddApplicationLog(string.Format(T("LogNodeExited"), code), LogSeverity.Warning);
            if (_miner.IsRunning)
            {
                _minerStopRequested = true;
                try { await _miner.StopAsync(_config.Miner.StopTimeoutSeconds, CancellationToken.None); } catch { }
                await ShutdownManagedKuboAsync();
                await RestorePowerAfterUnexpectedExitAsync();
                AddApplicationLog(T("LogMinerStoppedNodeExit"), LogSeverity.Warning);
            }
        }
        RaiseProcessState();
    }
    private void RaiseProcessState()
    {
        Raise(nameof(IsMinerRunning)); Raise(nameof(IsNodeRunning)); Raise(nameof(HasActiveProcesses)); Raise(nameof(ConfigurationEnabled)); Raise(nameof(ConnectionConfigurationEnabled)); Raise(nameof(NodeControlsEnabled)); RefreshCommands();
    }
    private void RefreshCommands()
    {
        StartCommand.Raise(); StopCommand.Raise(); StartNodeCommand.Raise(); StopNodeCommand.Raise(); ApplyPowerCommand.Raise(); RefreshCommand.Raise();
    }

    private void ResetExternalNodeSyncState()
    {
        if (IsPoolMode || _miner.IsRunning || _node.IsOwnedRunning) return;
        NodeSyncKnown = false;
        NodeSyncPercent = null;
        _lastDisplayedSyncPercent = -1;
        _nodeReachable = false;
        _nodeSyncTracker.Reset(DateTime.UtcNow);
    }

    public void SetWindowPlacement(double width, double height, double left, double top, bool maximized)
    {
        if (width >= 960) _settings.WindowWidth = width;
        if (height >= 670) _settings.WindowHeight = height;
        _settings.WindowLeft = left; _settings.WindowTop = top; _settings.WindowMaximized = maximized;
    }

    private async Task SaveSettingsAsync()
    {
        _settings.Language = Language; _settings.Wallet = Wallet; _settings.MiningMode = IsPoolMode ? "pool" : "solo";
        _settings.NodeAddress = NodeAddress; _settings.NodePort = NodePort; _settings.PoolAddress = PoolAddress;
        _settings.Gpus = Gpus.ToDictionary(x => x.Uuid, x => new GpuPreference { Selected = x.IsSelected, TierId = x.SelectedTier?.Id ?? "auto", PowerLimit = x.PowerLimit }, StringComparer.OrdinalIgnoreCase);
        await _settingsService.SaveAsync(_settings, CancellationToken.None);
    }

    public async Task ShutdownAsync()
    {
        if (_shutdownStarted) return;
        _shutdownStarted = true;
        _minerStopRequested = true;
        _nodeStopRequested = true;
        try { await _miner.StopAsync(_config.Miner.StopTimeoutSeconds, CancellationToken.None); } catch { }
        try { await ShutdownManagedKuboAsync(); } catch { }
        try { await RestoreAllPowerLimitsAsync(false); } catch { }
        try { await _node.StopAsync(CancellationToken.None); } catch { }
        try { await SaveSettingsAsync(); } catch { }
        _lifetime.Cancel();
        await _miner.DisposeAsync(); await _node.DisposeAsync(); _statsApi.Dispose(); _lifetime.Dispose();
    }

    public async ValueTask DisposeAsync() => await ShutdownAsync();
    private sealed record PowerChange(double InitialWatts, double AppliedWatts, int GpuIndex);
}
