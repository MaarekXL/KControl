using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeryxControl.Models;

namespace KeryxControl.Services;

public static class TurzxDashboardRenderer
{
    private static readonly Typeface Semibold = new(new FontFamily("Segoe UI Semibold"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static TurzxRenderedFrame Render(TurzxDisplaySnapshot snapshot) =>
        Render(snapshot, TurzxProtocol.DisplayWidth, TurzxProtocol.DisplayHeight);

    public static TurzxRenderedFrame Render(TurzxDisplaySnapshot snapshot, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (width is < 240 or > 4096 || height is < 240 or > 2160) throw new ArgumentOutOfRangeException(nameof(width));

        var culture = CultureInfo.GetCultureInfo(snapshot.Language.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "fr-FR");
        var english = culture.TwoLetterISOLanguageName == "en";
        var scale = Math.Min(width / 480d, height / 320d);
        var margin = Math.Max(12d * scale, Math.Min(width, height) * .025);
        var headerHeight = 50d * scale;
        var gap = 10d * scale;
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brush("#050806"), null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(Brush("#071109"), null, new Rect(0, 0, width, headerHeight));
            drawing.DrawRectangle(Brush("#17301E"), null, new Rect(0, headerHeight - scale, width, Math.Max(1, scale)));
            drawing.DrawRoundedRectangle(Brush("#102B18"), new Pen(Brush("#22E66D"), Math.Max(1, scale)),
                new Rect(margin, 8 * scale, 34 * scale, 34 * scale), 8 * scale, 8 * scale);
            DrawText(drawing, "K", 23 * scale, Bold, Brush("#22E66D"), new Rect(margin, 9 * scale, 34 * scale, 32 * scale), culture, TextAlignment.Center);
            DrawText(drawing, "KERYX MINER", 18 * scale, Bold, Brush("#E9FFF0"),
                new Rect(margin + 44 * scale, 5 * scale, width * .45, 28 * scale), culture);
            DrawText(drawing, snapshot.SelectedGpuCount == 1 ? "1 GPU" : $"{snapshot.SelectedGpuCount} GPU", 10 * scale, Semibold,
                Brush("#5EA977"), new Rect(margin + 45 * scale, 30 * scale, width * .28, 14 * scale), culture);

            var statusBrush = snapshot.Level switch
            {
                TurzxDisplayLevel.Error => Brush("#FF6B73"),
                TurzxDisplayLevel.Warning => Brush("#F2B84B"),
                _ => Brush("#22E66D")
            };
            drawing.DrawEllipse(statusBrush, null, new Point(width * .65, 25 * scale), 4 * scale, 4 * scale);
            DrawText(drawing, snapshot.StateLabel.ToUpper(culture), 12 * scale, Semibold, statusBrush,
                new Rect(width * .66, 15 * scale, width * .31 - margin, 22 * scale), culture, TextAlignment.Right);

            if (width == height)
                DrawRoundLayout(drawing, snapshot, culture, english, width, scale);
            else if ((double)width / height >= 2.2)
                DrawWideLayout(drawing, snapshot, culture, english, width, height, margin, headerHeight, gap, scale);
            else
                DrawRegularLayout(drawing, snapshot, culture, english, width, height, margin, headerHeight, gap, scale);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        return new TurzxRenderedFrame(width, height, stride, pixels);
    }

    private static void DrawRoundLayout(DrawingContext drawing, TurzxDisplaySnapshot snapshot, CultureInfo culture,
        bool english, int size, double scale)
    {
        drawing.DrawRectangle(Brush("#050806"), null, new Rect(0, 0, size, size));
        DrawText(drawing, "KERYX MINER", 18 * scale, Bold, Brush("#E9FFF0"),
            new Rect(size * .27, 24 * scale, size * .46, 28 * scale), culture, TextAlignment.Center);
        DrawText(drawing, snapshot.SelectedGpuCount == 1 ? "1 GPU" : $"{snapshot.SelectedGpuCount} GPU", 9 * scale,
            Semibold, Brush("#5EA977"), new Rect(size * .35, 50 * scale, size * .30, 15 * scale), culture, TextAlignment.Center);

        var hashRect = new Rect(size * .095, 76 * scale, size * .81, 126 * scale);
        DrawCard(drawing, hashRect, scale);
        DrawText(drawing, "HASHRATE", 10 * scale, Semibold, Brush("#789184"),
            new Rect(hashRect.X + 12, hashRect.Y + 12, hashRect.Width - 24, 17), culture, TextAlignment.Center);
        DrawText(drawing, snapshot.Hashrate, 34 * scale, Bold, Brush("#E9FFF0"),
            new Rect(hashRect.X + 10, hashRect.Y + 50, hashRect.Width - 20, 50), culture, TextAlignment.Center);

        var twoWidth = size * .37;
        var firstX = size * .12;
        var secondX = size * .51;
        DrawMetricCard(drawing, new Rect(firstX, 212 * scale, twoWidth, 86 * scale),
            english ? "GPU MAX" : "GPU MAX.", snapshot.Temperature, culture, scale);
        DrawMetricCard(drawing, new Rect(secondX, 212 * scale, twoWidth, 86 * scale),
            english ? "POWER" : "PUISSANCE", snapshot.Power, culture, scale);

        var threeWidth = size * .235;
        var threeGap = size * .018;
        var threeX = size * .129;
        DrawMetricCard(drawing, new Rect(threeX, 308 * scale, threeWidth, 88 * scale),
            english ? "GPU LOAD" : "CHARGE GPU", snapshot.Utilization, culture, scale);
        DrawMetricCard(drawing, new Rect(threeX + threeWidth + threeGap, 308 * scale, threeWidth, 88 * scale),
            english ? "BLOCKS" : "BLOCS A/R", $"{snapshot.Accepted}/{snapshot.Rejected}", culture, scale);
        DrawMetricCard(drawing, new Rect(threeX + (threeWidth + threeGap) * 2, 308 * scale, threeWidth, 88 * scale),
            english ? "UPTIME" : "DURÉE", snapshot.Uptime, culture, scale);

        var statusBrush = snapshot.Level switch
        {
            TurzxDisplayLevel.Error => Brush("#FF6B73"),
            TurzxDisplayLevel.Warning => Brush("#F2B84B"),
            _ => Brush("#22E66D")
        };
        DrawText(drawing, snapshot.StateLabel.ToUpper(culture), 10 * scale, Semibold, statusBrush,
            new Rect(size * .22, 415 * scale, size * .56, 18 * scale), culture, TextAlignment.Center);
    }

    private static void DrawRegularLayout(DrawingContext drawing, TurzxDisplaySnapshot snapshot, CultureInfo culture,
        bool english, int width, int height, double margin, double headerHeight, double gap, double scale)
    {
        var mainTop = headerHeight + gap;
        var bottomHeight = Math.Max(72 * scale, height * .305);
        var mainHeight = height - mainTop - bottomHeight - gap - margin;
        var usableWidth = width - margin * 2;
        var hashWidth = usableWidth * .635;
        var tempWidth = usableWidth - hashWidth - gap;
        var hashRect = new Rect(margin, mainTop, hashWidth, mainHeight);
        var tempRect = new Rect(hashRect.Right + gap, mainTop, tempWidth, mainHeight);

        DrawHashCard(drawing, hashRect, snapshot, culture, english, scale);
        DrawTemperatureCard(drawing, tempRect, snapshot, culture, english, scale);

        var bottomTop = mainTop + mainHeight + gap;
        var metricWidth = (usableWidth - gap * 3) / 4;
        DrawMetricCard(drawing, new Rect(margin, bottomTop, metricWidth, bottomHeight), english ? "POWER" : "PUISSANCE", snapshot.Power, culture, scale);
        DrawMetricCard(drawing, new Rect(margin + metricWidth + gap, bottomTop, metricWidth, bottomHeight), english ? "GPU LOAD" : "CHARGE GPU", snapshot.Utilization, culture, scale);
        DrawMetricCard(drawing, new Rect(margin + (metricWidth + gap) * 2, bottomTop, metricWidth, bottomHeight), english ? "BLOCKS A/R" : "BLOCS A/R", $"{snapshot.Accepted}/{snapshot.Rejected}", culture, scale);
        DrawMetricCard(drawing, new Rect(margin + (metricWidth + gap) * 3, bottomTop, metricWidth, bottomHeight), english ? "UPTIME" : "DURÉE", snapshot.Uptime, culture, scale);
    }

    private static void DrawWideLayout(DrawingContext drawing, TurzxDisplaySnapshot snapshot, CultureInfo culture,
        bool english, int width, int height, double margin, double headerHeight, double gap, double scale)
    {
        var top = headerHeight + gap;
        var cardHeight = height - top - margin;
        var usableWidth = width - margin * 2 - gap * 5;
        var hashWidth = usableWidth * .29;
        var tempWidth = usableWidth * .15;
        var metricWidth = (usableWidth - hashWidth - tempWidth) / 4;
        var x = margin;
        DrawHashCard(drawing, new Rect(x, top, hashWidth, cardHeight), snapshot, culture, english, scale);
        x += hashWidth + gap;
        DrawTemperatureCard(drawing, new Rect(x, top, tempWidth, cardHeight), snapshot, culture, english, scale);
        x += tempWidth + gap;
        DrawMetricCard(drawing, new Rect(x, top, metricWidth, cardHeight), english ? "POWER" : "PUISSANCE", snapshot.Power, culture, scale);
        x += metricWidth + gap;
        DrawMetricCard(drawing, new Rect(x, top, metricWidth, cardHeight), english ? "GPU LOAD" : "CHARGE GPU", snapshot.Utilization, culture, scale);
        x += metricWidth + gap;
        DrawMetricCard(drawing, new Rect(x, top, metricWidth, cardHeight), english ? "BLOCKS A/R" : "BLOCS A/R", $"{snapshot.Accepted}/{snapshot.Rejected}", culture, scale);
        x += metricWidth + gap;
        DrawMetricCard(drawing, new Rect(x, top, metricWidth, cardHeight), english ? "UPTIME" : "DURÉE", snapshot.Uptime, culture, scale);
    }

    private static void DrawHashCard(DrawingContext drawing, Rect rect, TurzxDisplaySnapshot snapshot,
        CultureInfo culture, bool english, double scale)
    {
        DrawCard(drawing, rect, scale);
        DrawText(drawing, "HASHRATE", 11 * scale, Semibold, Brush("#789184"), new Rect(rect.X + 14 * scale, rect.Y + 12 * scale, rect.Width - 28 * scale, 18 * scale), culture);
        DrawText(drawing, snapshot.Hashrate, 38 * scale, Bold, Brush("#E9FFF0"),
            new Rect(rect.X + 12 * scale, rect.Y + rect.Height * .30, rect.Width - 24 * scale, 55 * scale), culture);
        DrawText(drawing, english ? "KERYX COMPUTE RATE" : "PUISSANCE DE CALCUL KERYX", 10 * scale, Semibold, Brush("#4F765C"),
            new Rect(rect.X + 14 * scale, rect.Bottom - 27 * scale, rect.Width - 28 * scale, 16 * scale), culture);
    }

    private static void DrawTemperatureCard(DrawingContext drawing, Rect rect, TurzxDisplaySnapshot snapshot,
        CultureInfo culture, bool english, double scale)
    {
        DrawCard(drawing, rect, scale);
        DrawText(drawing, english ? "GPU MAX" : "GPU MAX.", 11 * scale, Semibold, Brush("#789184"),
            new Rect(rect.X + 10 * scale, rect.Y + 12 * scale, rect.Width - 20 * scale, 18 * scale), culture, TextAlignment.Center);
        DrawText(drawing, snapshot.Temperature, 34 * scale, Bold, Brush("#8BFFAE"),
            new Rect(rect.X + 8 * scale, rect.Y + rect.Height * .32, rect.Width - 16 * scale, 52 * scale), culture, TextAlignment.Center);
        DrawText(drawing, english ? "TEMPERATURE" : "TEMPÉRATURE", 9 * scale, Semibold, Brush("#4F765C"),
            new Rect(rect.X + 8 * scale, rect.Bottom - 27 * scale, rect.Width - 16 * scale, 16 * scale), culture, TextAlignment.Center);
    }

    private static void DrawCard(DrawingContext drawing, Rect rect, double scale) =>
        drawing.DrawRoundedRectangle(Brush("#0B110D"), new Pen(Brush("#1B3423"), Math.Max(1, scale)), rect, 13 * scale, 13 * scale);

    private static void DrawMetricCard(DrawingContext drawing, Rect rect, string label, string value, CultureInfo culture, double scale)
    {
        DrawCard(drawing, rect, scale);
        DrawText(drawing, label, 9 * scale, Semibold, Brush("#789184"), new Rect(rect.X + 8 * scale, rect.Y + 13 * scale, rect.Width - 16 * scale, 16 * scale), culture, TextAlignment.Center);
        DrawText(drawing, value, 18 * scale, Bold, Brush("#E9FFF0"), new Rect(rect.X + 7 * scale, rect.Y + rect.Height * .48, rect.Width - 14 * scale, 31 * scale), culture, TextAlignment.Center);
    }

    private static void DrawText(DrawingContext drawing, string text, double size, Typeface typeface, System.Windows.Media.Brush brush, Rect bounds, CultureInfo culture, TextAlignment alignment = TextAlignment.Left)
    {
        var formatted = new FormattedText(text ?? "", culture, FlowDirection.LeftToRight, typeface, size, brush, 1)
        {
            MaxTextWidth = Math.Max(1, bounds.Width),
            MaxTextHeight = Math.Max(1, bounds.Height),
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = alignment
        };
        drawing.DrawText(formatted, new Point(bounds.X, bounds.Y));
    }

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
