using KeryxControl.Infrastructure;
using KeryxControl.Models;

namespace KeryxControl.ViewModels;

public sealed class MiningModeOptionViewModel(string id, string nameFr, string nameEn, string language) : ObservableObject
{
    private string _language = language;
    public string Id { get; } = id;
    public string DisplayName => _language == "en" ? nameEn : nameFr;
    public void SetLanguage(string language) { _language = language; Raise(nameof(DisplayName)); }
}

public sealed class TierOptionViewModel(TierConfig config, string language) : ObservableObject
{
    private string _language = language;
    public TierConfig Config { get; } = config;
    public string Id => Config.Id;
    public bool IsAuto => Config.IsAuto;
    public string DisplayName
    {
        get
        {
            if (IsAuto) return _language == "en" ? "Auto — best fit per GPU" : "Auto — adapté à chaque GPU";
            var localized = _language == "en" ? Config.NameEn : Config.NameFr;
            if (string.IsNullOrWhiteSpace(localized)) localized = Config.Name;
            return string.IsNullOrWhiteSpace(Config.Model) ? localized : $"{localized} — {Config.Model}";
        }
    }
    public void SetLanguage(string language) { _language = language; Raise(nameof(DisplayName)); }
}

public sealed class GpuDeviceViewModel : ObservableObject
{
    private bool _isSelected = true;
    private string _language = "fr";
    private TierOptionViewModel? _selectedTier;
    private double _powerLimit;
    private string _temperature = "— °C", _memoryTemperature = "— °C", _power = "— W", _utilization = "— %", _memory = "—", _fan = "— %", _hashrate = "0.00 MH/s";
    private long _accepted, _rejected;

    public GpuDeviceViewModel(GpuInfo info) { Info = info; _powerLimit = info.PowerLimitW; }
    public GpuInfo Info { get; }
    public int Index => Info.Index;
    public string Uuid => Info.Uuid;
    public string DisplayName => Info.DisplayName;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public TierOptionViewModel? SelectedTier { get => _selectedTier; set => Set(ref _selectedTier, value); }
    public double PowerLimit { get => _powerLimit; set => Set(ref _powerLimit, Math.Clamp(value, Info.PowerMinW, Info.PowerMaxW)); }
    public string Temperature { get => _temperature; private set => Set(ref _temperature, value); }
    public string MemoryTemperature { get => _memoryTemperature; private set { if (Set(ref _memoryTemperature, value)) Raise(nameof(MemoryTemperatureDisplay)); } }
    public string MemoryTemperatureDisplay => MemoryTemperature == "— °C"
        ? (_language == "en" ? "Memory junction: —" : "Jonction mémoire : —")
        : (_language == "en" ? $"Memory junction: {MemoryTemperature}" : $"Jonction mémoire : {MemoryTemperature}");
    public string Power { get => _power; private set => Set(ref _power, value); }
    public string Utilization { get => _utilization; private set => Set(ref _utilization, value); }
    public string Memory { get => _memory; private set => Set(ref _memory, value); }
    public string Fan { get => _fan; private set => Set(ref _fan, value); }
    public string Hashrate { get => _hashrate; private set => Set(ref _hashrate, value); }
    public long Accepted { get => _accepted; private set => Set(ref _accepted, value); }
    public long Rejected { get => _rejected; private set => Set(ref _rejected, value); }
    public string DetailText => $"{Hashrate}  •  {Temperature}  •  {Power}  •  {(_language == "en" ? "Fan" : "Vent.")} {Fan}  •  A/R {Accepted}/{Rejected}";

    public void SetLanguage(string language)
    {
        _language = language;
        Raise(nameof(DetailText));
        Raise(nameof(MemoryTemperatureDisplay));
    }

    public void ApplyNvidiaMetrics(GpuMetrics metrics)
    {
        Temperature = $"{metrics.TemperatureC:0} °C";
        Power = $"{metrics.PowerW:0.0} W";
        Utilization = $"{metrics.UtilizationPercent:0} %";
        Memory = $"{metrics.MemoryUsedMiB} / {metrics.MemoryTotalMiB} MiB";
        Fan = metrics.FanPercent > 0 ? $"{metrics.FanPercent:0} %" : "— %";
        Raise(nameof(DetailText));
    }

    public void ApplyMinerStats(MinerApiDevice stats)
    {
        Hashrate = $"{stats.HashrateHs / 1_000_000d:0.00} MH/s";
        Accepted = stats.BlocksAccepted;
        Rejected = stats.BlocksRejected;
        if (stats.TemperatureC is > 0) Temperature = $"{stats.TemperatureC:0} °C";
        if (stats.MemoryTemperatureC is > 0) MemoryTemperature = $"{stats.MemoryTemperatureC:0} °C";
        if (stats.PowerDrawW is > 0) Power = $"{stats.PowerDrawW:0.0} W";
        if (stats.FanPercent is > 0) Fan = $"{stats.FanPercent:0} %";
        Raise(nameof(DetailText));
    }

    public void ResetMinerStats(bool resetBlocks)
    {
        Hashrate = "0.00 MH/s";
        if (resetBlocks) { Accepted = 0; Rejected = 0; }
        Raise(nameof(DetailText));
    }
}
