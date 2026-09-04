using KeryxControl.Models;
using KeryxControl.Services;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class TurzxHardwareTest
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var snapshot = new TurzxDisplaySnapshot(
                "fr", "TEST MATÉRIEL", TurzxDisplayLevel.Normal,
                "1.23 MH/s", "62 °C", "187 W", "94 %", "00:42:17", 12, 0, 1);
            if (args.Length > 0 && args[0].Equals("--all-previews", StringComparison.OrdinalIgnoreCase))
            {
                var directory = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "turzx-previews");
                foreach (var profile in TurzxDisplayCatalog.Profiles.GroupBy(x => (x.LandscapeWidth, x.LandscapeHeight)).Select(x => x.First()))
                {
                    var preview = TurzxDashboardRenderer.Render(snapshot, profile.LandscapeWidth, profile.LandscapeHeight);
                    SavePreview(preview, Path.Combine(directory, $"{profile.LandscapeWidth}x{profile.LandscapeHeight}.png"));
                }
                Console.WriteLine($"Aperçus TURZX générés dans {Path.GetFullPath(directory)}.");
                return 0;
            }
            var frame = TurzxDashboardRenderer.Render(snapshot);
            if (args.Length > 0) SavePreview(frame, args[0]);
            var port = TurzxDisplayService.SendHardwareTestFrame(frame, 70);
            Console.WriteLine($"TURZX USB35INCHIPSV2 validé sur {port}: image 480x320 envoyée avec succès.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Échec du test TURZX : " + ex.Message);
            return 1;
        }
    }

    private static void SavePreview(TurzxRenderedFrame frame, string path)
    {
        var bitmap = BitmapSource.Create(
            frame.Width, frame.Height, 96, 96, PixelFormats.Pbgra32, null,
            frame.BgraPixels, frame.Stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
