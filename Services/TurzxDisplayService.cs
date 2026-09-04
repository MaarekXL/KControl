using System.IO;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class TurzxDisplayService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _worker;
    private TurzxDisplaySnapshot? _latestSnapshot;
    private bool _enabled;
    private string _modelSetting = TurzxDisplayCatalog.AutoId;
    private string _portSetting = "AUTO";
    private int _brightness = 70;
    private int _reconnectGeneration;
    private TurzxConnectionStatus? _lastStatus;

    public TurzxDisplayService() => _worker = Task.Run(() => RunAsync(_lifetime.Token));

    public event Action<TurzxConnectionStatus>? StatusChanged;

    public void Configure(bool enabled, string? modelSetting, string? portSetting, int brightness)
    {
        lock (_gate)
        {
            var normalizedModel = TurzxDisplayCatalog.Find(modelSetting)?.Id ?? TurzxDisplayCatalog.AutoId;
            var normalizedPort = string.IsNullOrWhiteSpace(portSetting) ? "AUTO" : portSetting.Trim().ToUpperInvariant();
            var requiresReconnect = _enabled != enabled
                || !_modelSetting.Equals(normalizedModel, StringComparison.OrdinalIgnoreCase)
                || !_portSetting.Equals(normalizedPort, StringComparison.OrdinalIgnoreCase);
            _enabled = enabled;
            _modelSetting = normalizedModel;
            _portSetting = normalizedPort;
            _brightness = Math.Clamp(brightness, 0, 100);
            if (requiresReconnect) _reconnectGeneration++;
        }
        Signal();
    }

    public void Publish(TurzxDisplaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate) _latestSnapshot = snapshot;
        Signal();
    }

    public void ForceReconnect()
    {
        lock (_gate) _reconnectGeneration++;
        Signal();
    }

    internal static string SendHardwareTestFrame(TurzxRenderedFrame frame, int brightness = 70, string portSetting = "AUTO")
    {
        var device = TurzxPortDetector.Find("TURZX-35-A", portSetting)
            ?? throw new IOException("Aucun écran TURZX USB35INCHIPSV2 n’a été détecté.");
        using var driver = TurzxDeviceDriverFactory.Open(device);
        driver.Initialize(brightness);
        driver.DisplayFrame(frame, null);
        return driver.Endpoint;
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        Signal();
        try { await _worker.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _signal.Dispose();
        _lifetime.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        ITurzxDeviceDriver? driver = null;
        TurzxRenderedFrame? previousFrame = null;
        var appliedModelSetting = "";
        var appliedPortSetting = "";
        var appliedBrightness = -1;
        var appliedReconnectGeneration = -1;
        var retryAt = DateTime.MinValue;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var wait = retryAt > DateTime.UtcNow ? retryAt - DateTime.UtcNow : TimeSpan.FromSeconds(2);
                if (wait < TimeSpan.FromMilliseconds(100)) wait = TimeSpan.FromMilliseconds(100);
                try { await _signal.WaitAsync(wait, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }

                bool enabled;
                string modelSetting;
                string portSetting;
                int brightness;
                int reconnectGeneration;
                TurzxDisplaySnapshot? snapshot;
                lock (_gate)
                {
                    enabled = _enabled;
                    modelSetting = _modelSetting;
                    portSetting = _portSetting;
                    brightness = _brightness;
                    reconnectGeneration = _reconnectGeneration;
                    snapshot = _latestSnapshot;
                }

                if (!enabled)
                {
                    if (driver is not null) { driver.ScreenOff(); driver.Dispose(); driver = null; }
                    previousFrame = null;
                    appliedModelSetting = modelSetting;
                    appliedPortSetting = portSetting;
                    appliedReconnectGeneration = reconnectGeneration;
                    Report(new(TurzxConnectionState.Disabled));
                    continue;
                }

                var reconnect = driver is null
                    || !appliedModelSetting.Equals(modelSetting, StringComparison.OrdinalIgnoreCase)
                    || !appliedPortSetting.Equals(portSetting, StringComparison.OrdinalIgnoreCase)
                    || appliedReconnectGeneration != reconnectGeneration;
                if (reconnect)
                {
                    driver?.Dispose();
                    driver = null;
                    previousFrame = null;
                    if (DateTime.UtcNow < retryAt) continue;

                    var device = TurzxPortDetector.Find(modelSetting, portSetting);
                    if (device is null)
                    {
                        Report(new(TurzxConnectionState.Searching));
                        retryAt = DateTime.UtcNow.AddSeconds(3);
                        continue;
                    }

                    try
                    {
                        Report(new(TurzxConnectionState.Connecting, device.Endpoint, device.Profile.DisplayName));
                        driver = TurzxDeviceDriverFactory.Open(device);
                        driver.Initialize(brightness);
                        appliedModelSetting = modelSetting;
                        appliedPortSetting = portSetting;
                        appliedReconnectGeneration = reconnectGeneration;
                        appliedBrightness = brightness;
                        retryAt = DateTime.MinValue;
                        Report(new(TurzxConnectionState.Connected, driver.Endpoint, driver.Profile.DisplayName));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        driver?.Dispose();
                        driver = null;
                        Report(new(TurzxConnectionState.Error, device.Endpoint, device.Profile.DisplayName, ex.Message));
                        retryAt = DateTime.UtcNow.AddSeconds(3);
                        continue;
                    }
                }

                if (driver is null || snapshot is null) continue;
                try
                {
                    if (appliedBrightness != brightness)
                    {
                        driver.SetBrightness(brightness);
                        appliedBrightness = brightness;
                    }
                    var frame = TurzxDashboardRenderer.Render(snapshot, driver.Profile.LandscapeWidth, driver.Profile.LandscapeHeight);
                    driver.DisplayFrame(frame, previousFrame);
                    previousFrame = frame;
                    Report(new(TurzxConnectionState.Connected, driver.Endpoint, driver.Profile.DisplayName));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
                {
                    var endpoint = driver.Endpoint;
                    var model = driver.Profile.DisplayName;
                    driver.Dispose();
                    driver = null;
                    previousFrame = null;
                    Report(new(TurzxConnectionState.Error, endpoint, model, ex.Message));
                    retryAt = DateTime.UtcNow.AddSeconds(3);
                }
            }
        }
        finally
        {
            driver?.Dispose();
        }
    }

    private void Signal()
    {
        try { if (_signal.CurrentCount == 0) _signal.Release(); } catch (ObjectDisposedException) { }
    }

    private void Report(TurzxConnectionStatus status)
    {
        if (status == _lastStatus) return;
        _lastStatus = status;
        StatusChanged?.Invoke(status);
    }
}
