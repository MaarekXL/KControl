using KeryxControl.Models;

namespace KeryxControl.Services;

public static class TurzxDisplayCatalog
{
    public const string AutoId = "AUTO";

    public static IReadOnlyList<TurzxDisplayProfile> Profiles { get; } =
    [
        new("TURZX-35-A", "TURZX 3,5\" · 480×320 · série A (validé)", TurzxProtocolFamily.SerialRevisionA,
            480, 320, 320, 480, 0x1a86, 0x5722, TurzxValidationLevel.HardwareValidated),

        new("TURZX-21-C", "TURZX 2,1\" rond · 480×480 · série C", TurzxProtocolFamily.SerialRevisionC,
            480, 480, 480, 480, 0x1a86, 0xca21, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-28-C", "TURZX 2,8\" rond · 480×480 · série C", TurzxProtocolFamily.SerialRevisionC,
            480, 480, 480, 480, 0x1a86, 0xca21, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-50-C", "TURZX 5,0\" · 800×480 · série C", TurzxProtocolFamily.SerialRevisionC,
            800, 480, 480, 800, 0x1a86, 0x5722, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-88-C", "TURZX 8,8\" · 1920×480 · série C", TurzxProtocolFamily.SerialRevisionC,
            1920, 480, 480, 1920, 0x1a86, 0xca21, TurzxValidationLevel.ProtocolValidated),

        new("TURZX-28-USB", "TURZX 2,8\" rond USB · 480×480", TurzxProtocolFamily.NativeUsb,
            480, 480, 480, 480, 0x1cbe, 0x0028, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-46-USB", "TURZX 4,6\" USB · 960×320", TurzxProtocolFamily.NativeUsb,
            960, 320, 320, 960, 0x1cbe, 0x0046, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-52-USB", "TURZX 5,2\" USB · 1280×720", TurzxProtocolFamily.NativeUsb,
            1280, 720, 720, 1280, 0x1cbe, 0x0050, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-80-USB", "TURZX 8,0\" USB · 1280×800", TurzxProtocolFamily.NativeUsb,
            1280, 800, 800, 1280, 0x1cbe, 0x0080, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-88-USB", "TURZX 8,8\" USB · 1920×480", TurzxProtocolFamily.NativeUsb,
            1920, 480, 480, 1920, 0x1cbe, 0x0088, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-92-USB", "TURZX 9,2\" USB · 1920×462", TurzxProtocolFamily.NativeUsb,
            1920, 462, 462, 1920, 0x1cbe, 0x0092, TurzxValidationLevel.ProtocolValidated),
        new("TURZX-123-USB", "TURZX 12,3\" USB · 1920×720", TurzxProtocolFamily.NativeUsb,
            1920, 720, 720, 1920, 0x1cbe, 0x0123, TurzxValidationLevel.ProtocolValidated)
    ];

    public static TurzxDisplayProfile? Find(string? id) => Profiles.FirstOrDefault(profile =>
        profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static TurzxDisplayProfile? FindNativeUsb(ushort vendorId, ushort productId) => Profiles.FirstOrDefault(profile =>
        profile.Protocol == TurzxProtocolFamily.NativeUsb && profile.VendorId == vendorId && profile.ProductId == productId);

    public static TurzxDisplayProfile HardwareValidatedProfile => Find("TURZX-35-A")!;
}
