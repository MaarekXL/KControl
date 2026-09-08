using System.IO;
using System.Text.RegularExpressions;
using KeryxControl.Models;
using Microsoft.Win32;

namespace KeryxControl.Services;

public static partial class TurzxPortDetector
{
    private const string UsbRegistryPath = @"SYSTEM\CurrentControlSet\Enum\USB";
    private static readonly string[] LegacySleepingSerials = ["CT21INCH", "CT88INCH", "USB7INCH"];

    public static TurzxDetectedDevice? Find(string? modelSetting, string? portSetting)
    {
        var requested = TurzxDisplayCatalog.Find(modelSetting);
        var port = string.IsNullOrWhiteSpace(portSetting) ? "AUTO" : portSetting.Trim().ToUpperInvariant();
        var devices = EnumerateUsbDevices();

        if (requested is not null)
        {
            if (requested.Protocol == TurzxProtocolFamily.NativeUsb)
                return devices.Any(device => device.VendorId == requested.VendorId && device.ProductId == requested.ProductId)
                    ? new TurzxDetectedDevice(requested, $"USB {requested.VendorId:X4}:{requested.ProductId:X4}")
                    : null;

            if (!port.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
                return IsValidPortName(port) ? new TurzxDetectedDevice(requested, port) : null;

            var serial = devices.FirstOrDefault(device => IsSerialMatch(requested, device)
                || requested.Protocol == TurzxProtocolFamily.SerialRevisionC && IsAwakeLegacyDevice(device));
            return serial?.PortName is { } name ? new TurzxDetectedDevice(requested, name, serial.SerialNumber) : null;
        }

        var revisionA = TurzxDisplayCatalog.HardwareValidatedProfile;
        var revisionADevice = devices.FirstOrDefault(device => IsSerialMatch(revisionA, device)
            && (port == "AUTO" || port.Equals(device.PortName, StringComparison.OrdinalIgnoreCase)));
        if (revisionADevice?.PortName is { } revisionAPort)
            return new TurzxDetectedDevice(revisionA, revisionAPort, revisionADevice.SerialNumber);

        foreach (var profile in TurzxDisplayCatalog.Profiles.Where(profile => profile.Protocol == TurzxProtocolFamily.NativeUsb))
            if (devices.Any(device => device.VendorId == profile.VendorId && device.ProductId == profile.ProductId))
                return new TurzxDetectedDevice(profile, $"USB {profile.VendorId:X4}:{profile.ProductId:X4}");

        foreach (var device in devices.Where(device => device.PortName is not null))
        {
            if (port != "AUTO" && !port.Equals(device.PortName, StringComparison.OrdinalIgnoreCase)) continue;
            var profile = GuessLegacyProfile(device.SerialNumber);
            if (profile is not null) return new TurzxDetectedDevice(profile, device.PortName!, device.SerialNumber);
        }

        return null;
    }

    public static string? FindUsb35InchPort() => Find("TURZX-35-A", "AUTO")?.Endpoint;

    public static string PrepareLegacyPort(TurzxDetectedDevice device)
    {
        if (device.Profile.Protocol != TurzxProtocolFamily.SerialRevisionC) return device.Endpoint;
        if (!IsSleepingLegacySerial(device.SerialNumber)) return device.Endpoint;

        try { using var wake = Win32SerialConnection.Open(device.Endpoint); }
        catch (IOException) { }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            Thread.Sleep(500);
            var awake = EnumerateUsbDevices().FirstOrDefault(IsAwakeLegacyDevice);
            if (awake?.PortName is not null) return awake.PortName;
        }
        throw new IOException("L’écran TURZX série C ne s’est pas réveillé après la connexion USB.");
    }

    public static bool IsValidPortName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) return false;
        return int.TryParse(value.AsSpan(3), out var number) && number is > 0 and <= 999;
    }

    internal static IReadOnlyList<UsbRegistryDevice> EnumerateUsbDevices()
    {
        var result = new List<UsbRegistryDevice>();
        if (!OperatingSystem.IsWindows()) return result;
        try
        {
            using var usb = Registry.LocalMachine.OpenSubKey(UsbRegistryPath);
            if (usb is null) return result;
            foreach (var hardwareKey in usb.GetSubKeyNames())
            {
                var match = VidPidRegex().Match(hardwareKey);
                if (!match.Success || !ushort.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out var vid)
                    || !ushort.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, null, out var pid)) continue;
                using var hardware = usb.OpenSubKey(hardwareKey);
                if (hardware is null) continue;
                foreach (var instanceName in hardware.GetSubKeyNames())
                {
                    using var instance = hardware.OpenSubKey(instanceName);
                    using var parameters = instance?.OpenSubKey("Device Parameters");
                    var portName = parameters?.GetValue("PortName") as string;
                    if (!IsValidPortName(portName)) portName = null;
                    var friendlyName = instance?.GetValue("FriendlyName") as string;
                    result.Add(new UsbRegistryDevice(vid, pid, instanceName, portName?.ToUpperInvariant(), friendlyName));
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return result;
        }
        return result;
    }

    private static bool IsSerialMatch(TurzxDisplayProfile profile, UsbRegistryDevice device)
    {
        if (device.PortName is null || device.VendorId != profile.VendorId || device.ProductId != profile.ProductId) return false;
        if (profile.Id.Equals("TURZX-35-A", StringComparison.OrdinalIgnoreCase))
            return device.SerialNumber.Contains("USB35INCH", StringComparison.OrdinalIgnoreCase)
                || device.FriendlyName?.Contains("USB35INCH", StringComparison.OrdinalIgnoreCase) == true;
        if (profile.Id.Equals("TURZX-50-C", StringComparison.OrdinalIgnoreCase))
            return device.SerialNumber.Equals("USB7INCH", StringComparison.OrdinalIgnoreCase) || IsAwakeLegacyDevice(device);
        return true;
    }

    private static TurzxDisplayProfile? GuessLegacyProfile(string? serialNumber)
    {
        // CT21INCH is unfortunately reused by both 2.1-inch and 8.8-inch units.
        // AUTO must not guess a resolution that could send an invalid frame.
        if (serialNumber?.Equals("CT88INCH", StringComparison.OrdinalIgnoreCase) == true) return TurzxDisplayCatalog.Find("TURZX-88-C");
        if (serialNumber?.Equals("USB7INCH", StringComparison.OrdinalIgnoreCase) == true) return TurzxDisplayCatalog.Find("TURZX-50-C");
        return null;
    }

    private static bool IsSleepingLegacySerial(string? value) =>
        value is not null && LegacySleepingSerials.Any(serial => serial.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static bool IsAwakeLegacyDevice(UsbRegistryDevice device) =>
        device.PortName is not null && (device.SerialNumber.Equals("20080411", StringComparison.OrdinalIgnoreCase)
            || device.VendorId == 0x0525 && device.ProductId == 0xa4a7
            || device.VendorId == 0x1d6b && device.ProductId is 0x0121 or 0x0106);

    [GeneratedRegex(@"^VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VidPidRegex();

    internal sealed record UsbRegistryDevice(ushort VendorId, ushort ProductId, string SerialNumber, string? PortName, string? FriendlyName);
}
