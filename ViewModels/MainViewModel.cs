using System.Collections.ObjectModel;
using System.IO;
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
    private readonly NvidiaService _nvidia = new(); private readonly MinerService _miner = new(); private readonly NodeService _node = new(); private readonly MinerLogParser _parser = new();
    private readonly CancellationTokenSource _lifetime = new(); private AppConfig _config = new(); private GpuInfo? _selectedGpu; private TierConfig? _selectedTier;
    private readonly object _pendingLogGate = new(); private readonly Queue<(string Line, bool ParseMiner)> _pendingLogs = new(); private bool _logDrainScheduled; private int _droppedLogCount; private string _lastNodeLine = ""; private DateTime _lastNodeLineAt = DateTime.MinValue;
    private string _wallet = "", _nodeAddress = "127.0.0.1", _status = "Initialisation…", _hashrate = "0 MH/s", _power = "— W", _temperature = "— °C", _utilization = "— %", _memory = "—", _efficiency = "— MH/W", _logText = "", _escrowPublicKey = "", _escrowCertificate = "", _escrowStatus = "";
    private int _nodePort = 22110;
    private string _statusKey = "StatusInitializing";
    private double _powerLimit; private int _accepted, _rejected; private MinerStats _stats = new();
    public ObservableCollection<GpuInfo> Gpus { get; } = []; public ObservableCollection<TierConfig> Tiers { get; } = []; public ObservableCollection<string> Logs { get; } = [];
    public GpuInfo? SelectedGpu { get => _selectedGpu; set { if (Set(ref _selectedGpu, value) && value is not null) { PowerLimit = value.PowerLimitW; SelectAutomaticTier(); } RefreshCommands(); } }
    public TierConfig? SelectedTier { get => _selectedTier; set => Set(ref _selectedTier, value); }
    public string Wallet { get => _wallet; set { Set(ref _wallet, value); RefreshCommands(); } } public string NodeAddress { get => _nodeAddress; set { Set(ref _nodeAddress, value); RefreshCommands(); } } public int NodePort { get => _nodePort; set => Set(ref _nodePort, value); }
    public string Status { get => _status; set => Set(ref _status, value); } public string Hashrate { get => _hashrate; set => Set(ref _hashrate, value); } public string Power { get => _power; set => Set(ref _power, value); }
    public string Temperature { get => _temperature; set => Set(ref _temperature, value); } public string Utilization { get => _utilization; set => Set(ref _utilization, value); } public string Memory { get => _memory; set => Set(ref _memory, value); }
    public string Efficiency { get => _efficiency; set => Set(ref _efficiency, value); } public int Accepted { get => _accepted; set => Set(ref _accepted, value); } public int Rejected { get => _rejected; set => Set(ref _rejected, value); }
    public string LogText { get => _logText; private set => Set(ref _logText, value); }
    public string EscrowPublicKey { get => _escrowPublicKey; private set => Set(ref _escrowPublicKey, value); }
    public string EscrowCertificate { get => _escrowCertificate; set => Set(ref _escrowCertificate, value); }
    public string EscrowStatus { get => _escrowStatus; private set => Set(ref _escrowStatus, value); }
    public double PowerLimit { get => _powerLimit; set => Set(ref _powerLimit, value); } public double PowerMinimum => SelectedGpu?.PowerMinW ?? 0; public double PowerMaximum => SelectedGpu?.PowerMaxW ?? 400;
    public AsyncCommand StartCommand { get; } public AsyncCommand StopCommand { get; } public AsyncCommand StartNodeCommand { get; } public AsyncCommand StopNodeCommand { get; } public AsyncCommand ApplyPowerCommand { get; } public AsyncCommand RefreshCommand { get; } public AsyncCommand CopyEscrowKeyCommand { get; } public AsyncCommand PasteEscrowCertCommand { get; } public AsyncCommand SaveEscrowCertCommand { get; }
    public MainViewModel()
    {
        StartCommand = new(StartAsync, () => !_miner.IsRunning && SelectedGpu is not null && !string.IsNullOrWhiteSpace(Wallet) && !string.IsNullOrWhiteSpace(NodeAddress)); StopCommand = new(StopAsync, () => _miner.IsRunning);
        StartNodeCommand = new(StartNodeAsync, () => !_node.IsOwnedRunning); StopNodeCommand = new(StopNodeAsync, () => _node.IsOwnedRunning);
        ApplyPowerCommand = new(ApplyPowerAsync, () => SelectedGpu is not null); RefreshCommand = new(DetectAsync);
        CopyEscrowKeyCommand = new(() => { try { if (!string.IsNullOrWhiteSpace(EscrowPublicKey)) Clipboard.SetText(EscrowPublicKey); } catch (Exception ex) { EscrowStatus = ex.Message; } return Task.CompletedTask; }, () => !string.IsNullOrWhiteSpace(EscrowPublicKey));
        PasteEscrowCertCommand = new(() => { try { if (Clipboard.ContainsText()) EscrowCertificate = Clipboard.GetText(); } catch (Exception ex) { EscrowStatus = ex.Message; } return Task.CompletedTask; });
        SaveEscrowCertCommand = new(SaveEscrowCertificateAsync);
        _miner.LineReceived += line => QueueExternalLog(line, true); _miner.Exited += code => Application.Current.Dispatcher.BeginInvoke(() => { _statusKey = "StatusStopped"; Status = $"{T(_statusKey)} (code {code})"; RefreshCommands(); });
        _node.LineReceived += line => QueueExternalLog(line, false);
    }
    public async Task InitializeAsync() { await LoadConfigAsync(); await DetectAsync(); _ = MonitorAsync(_lifetime.Token); }
    private async Task LoadConfigAsync() { var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json"); try { _config = JsonSerializer.Deserialize<AppConfig>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); } catch (Exception ex) { AddLog($"Configuration invalide : {ex.Message}"); } Tiers.Clear(); foreach (var t in _config.Tiers.OrderBy(x => x.MinVramGb)) Tiers.Add(t); }
    private async Task DetectAsync() { try { SetStatus("StatusDetecting"); var current = SelectedGpu?.Uuid; var gpus = await _nvidia.DetectAsync(_lifetime.Token); Gpus.Clear(); foreach (var g in gpus) Gpus.Add(g); SelectedGpu = Gpus.FirstOrDefault(g => g.Uuid == current) ?? Gpus.FirstOrDefault(); SetStatus(Gpus.Count == 0 ? "StatusNoGpu" : await IsNodeReachableAsync(NodeAddress, NodePort, _lifetime.Token) ? "StatusNodeOnline" : "StatusReady"); } catch (Exception ex) { SetStatus("StatusNvidiaUnavailable"); AddLog(ex.Message); } }
    private async Task StartAsync() { try { if (SelectedGpu is null) return; if (!await IsNodeReachableAsync(NodeAddress.Trim(), NodePort, _lifetime.Token)) { SetStatus("StatusNodeUnavailable"); AddLog(string.Format(T("LogNodeMissing"), NodeAddress, NodePort)); return; } _stats = new(); await _miner.StartAsync(_config.Miner, Wallet.Trim(), NodeAddress.Trim(), NodePort, SelectedGpu.Index, SelectedTier?.Argument ?? "", _lifetime.Token); SetStatus("StatusMining"); AddLog(T("LogMinerStarted")); } catch (Exception ex) { SetStatus("StatusStartFailed"); AddLog(ex.Message); } finally { RefreshCommands(); } }
    private static async Task<bool> IsNodeReachableAsync(string host, int port, CancellationToken ct) { try { using var client = new TcpClient(); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(2)); await client.ConnectAsync(host, port, timeout.Token); return true; } catch { return false; } }
    private async Task StopAsync() { try { SetStatus("StatusStopping"); await _miner.StopAsync(_config.Miner.StopTimeoutSeconds, _lifetime.Token); SetStatus("StatusStopped"); AddLog(T("LogMinerStopped")); } catch (Exception ex) { AddLog(ex.Message); } finally { RefreshCommands(); } }
    private async Task StartNodeAsync() { try { SetStatus("StatusStartingNode"); await _node.StartAsync(_config.Node, NodeAddress.Trim(), NodePort, _lifetime.Token); AddLog(T("LogNodeStarted")); await Task.Delay(1200, _lifetime.Token); SetStatus(await IsNodeReachableAsync(NodeAddress, NodePort, _lifetime.Token) ? "StatusNodeOnline" : "StatusStartingNode"); } catch (FileNotFoundException) { SetStatus("StatusNodeUnavailable"); AddLog(T("LogNodeMissingExe")); } catch (Exception ex) { SetStatus("StatusStartFailed"); AddLog(ex.Message); } finally { RefreshCommands(); } }
    private async Task StopNodeAsync() { try { await _node.StopAsync(_lifetime.Token); SetStatus("StatusNodeUnavailable"); AddLog(T("LogNodeStopped")); } catch (Exception ex) { AddLog(ex.Message); } finally { RefreshCommands(); } }
    private async Task ApplyPowerAsync() { if (SelectedGpu is null) return; try { await _nvidia.SetPowerLimitAsync(SelectedGpu, PowerLimit, _lifetime.Token); AddLog($"Power limit réglé à {PowerLimit:0.#} W (droits administrateur NVIDIA requis)." ); } catch (Exception ex) { AddLog($"Power limit : {ex.Message}"); } }
    private async Task MonitorAsync(CancellationToken ct) { using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2)); try { while (await timer.WaitForNextTickAsync(ct)) { var gpu = SelectedGpu; if (gpu is null) continue; try { var m = await _nvidia.GetMetricsAsync(gpu.Index, ct); Application.Current.Dispatcher.Invoke(() => { Temperature = $"{m.TemperatureC:0} °C"; Power = $"{m.PowerW:0.0} W"; Utilization = $"{m.UtilizationPercent:0} %"; Memory = $"{m.MemoryUsedMiB} / {m.MemoryTotalMiB} MiB"; Efficiency = m.PowerW > 0 ? $"{_stats.HashrateMh / m.PowerW:0.00} MH/W" : "— MH/W"; }); } catch (Exception ex) { Application.Current.Dispatcher.Invoke(() => AddLog($"Monitoring : {ex.Message}")); await Task.Delay(5000, ct); } } } catch (OperationCanceledException) { } }
    private async Task SaveEscrowCertificateAsync() { var match = Regex.Match(EscrowCertificate, @"(?i)(?<![0-9a-f])[0-9a-f]{128}(?![0-9a-f])"); if (!match.Success) { EscrowStatus = T("EscrowInvalid"); return; } try { var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_config.Miner.Executable), AppContext.BaseDirectory); var folder = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory; Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, "escrow.cert"), match.Value.ToLowerInvariant()); EscrowCertificate = match.Value.ToLowerInvariant(); EscrowStatus = T("EscrowSaved"); AddLog(T("EscrowSavedLog"), false); } catch (Exception ex) { EscrowStatus = ex.Message; } }
    private void QueueExternalLog(string line, bool parseMiner)
    {
        var schedule = false;
        lock (_pendingLogGate)
        {
            if (!parseMiner && line == _lastNodeLine && DateTime.UtcNow - _lastNodeLineAt < TimeSpan.FromSeconds(1)) return;
            if (!parseMiner) { _lastNodeLine = line; _lastNodeLineAt = DateTime.UtcNow; }
            if (_pendingLogs.Count >= 2_000) { _pendingLogs.Dequeue(); _droppedLogCount++; }
            _pendingLogs.Enqueue((line, parseMiner));
            if (!_logDrainScheduled) { _logDrainScheduled = true; schedule = true; }
        }
        if (schedule && Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DrainPendingLogs));
    }
    private void DrainPendingLogs()
    {
        var batch = new List<(string Line, bool ParseMiner)>(250);
        var dropped = 0;
        var more = false;
        lock (_pendingLogGate)
        {
            while (batch.Count < 250 && _pendingLogs.Count > 0) batch.Add(_pendingLogs.Dequeue());
            dropped = _droppedLogCount; _droppedLogCount = 0;
            more = _pendingLogs.Count > 0;
            if (!more) _logDrainScheduled = false;
        }
        foreach (var item in batch) AddLogCore(item.Line, item.ParseMiner);
        if (dropped > 0) AddLogCore(string.Format(T("LogLinesSkipped"), dropped), false);
        RebuildLogText();
        if (more && Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DrainPendingLogs));
    }
    private void AddLog(string line, bool parseMiner = false) { AddLogCore(line, parseMiner); RebuildLogText(); }
    private void AddLogCore(string line, bool parseMiner)
    {
        Logs.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
        while (Logs.Count > 500) Logs.RemoveAt(0);
        CaptureEscrowKey(line);
        if (!parseMiner) return;
        _stats = _parser.Parse(line, _stats);
        Hashrate = $"{_stats.HashrateMh:0.00} MH/s"; Accepted = _stats.Accepted; Rejected = _stats.Rejected;
    }
    private void RebuildLogText() => LogText = string.Join(Environment.NewLine, Logs);
    private void CaptureEscrowKey(string line) { var labelled = Regex.Match(line, @"(?i)(?:escrow pubkey\s*:|paste this escrow key\s*:?)\s*([0-9a-f]{64})"); var any = Regex.Match(line, @"(?i)(?<![0-9a-f])([0-9a-f]{64})(?![0-9a-f])"); var value = labelled.Success ? labelled.Groups[1].Value : line.Trim().Length <= 80 && any.Success ? any.Groups[1].Value : ""; if (value.Length != 64) return; EscrowPublicKey = value.ToLowerInvariant(); EscrowStatus = T("EscrowKeyDetected"); CopyEscrowKeyCommand.Raise(); }
    private void SelectAutomaticTier() { if (SelectedGpu is null) return; var gb = SelectedGpu.MemoryTotalMiB / 1024d; SelectedTier = Tiers.Where(t => t.MinVramGb <= gb + 0.25).OrderByDescending(t => t.MinVramGb).FirstOrDefault() ?? Tiers.FirstOrDefault(); Raise(nameof(PowerMinimum)); Raise(nameof(PowerMaximum)); }
    public void SetLanguage() { Status = T(_statusKey); if (string.IsNullOrWhiteSpace(EscrowStatus)) EscrowStatus = T("EscrowWaiting"); }
    private static string T(string key) => Application.Current.TryFindResource(key) as string ?? key;
    private void SetStatus(string key) { _statusKey = key; Status = T(key); }
    private void RefreshCommands() { StartCommand.Raise(); StopCommand.Raise(); StartNodeCommand.Raise(); StopNodeCommand.Raise(); ApplyPowerCommand.Raise(); }
    public async ValueTask DisposeAsync() { _lifetime.Cancel(); await _miner.DisposeAsync(); await _node.DisposeAsync(); _lifetime.Dispose(); }
}
