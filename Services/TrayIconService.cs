using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using KeryxControl.Models;
using WinForms = System.Windows.Forms;

namespace KeryxControl.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _openItem;
    private readonly WinForms.ToolStripMenuItem _startItem;
    private readonly WinForms.ToolStripMenuItem _stopItem;
    private readonly WinForms.ToolStripMenuItem _exitItem;
    private readonly Dictionary<TrayIconState, Icon> _icons;
    private bool _disposed;
    private TrayIconState _state = TrayIconState.Stopped;
    private string _language = "";

    public TrayIconService()
    {
        _icons = new()
        {
            [TrayIconState.Stopped] = CreateIcon(Color.FromArgb(125, 145, 132)),
            [TrayIconState.Warning] = CreateIcon(Color.FromArgb(242, 184, 75)),
            [TrayIconState.Mining] = CreateIcon(Color.FromArgb(34, 230, 109)),
            [TrayIconState.Error] = CreateIcon(Color.FromArgb(255, 107, 115))
        };

        _openItem = new WinForms.ToolStripMenuItem();
        _startItem = new WinForms.ToolStripMenuItem();
        _stopItem = new WinForms.ToolStripMenuItem();
        _exitItem = new WinForms.ToolStripMenuItem();
        _openItem.Font = new Font(_openItem.Font, System.Drawing.FontStyle.Bold);
        _openItem.Click += (_, _) => OpenRequested?.Invoke();
        _startItem.Click += (_, _) => StartRequested?.Invoke();
        _stopItem.Click += (_, _) => StopRequested?.Invoke();
        _exitItem.Click += (_, _) => ExitRequested?.Invoke();

        _menu = new WinForms.ContextMenuStrip();
        _menu.Items.AddRange([
            _openItem,
            new WinForms.ToolStripSeparator(),
            _startItem,
            _stopItem,
            new WinForms.ToolStripSeparator(),
            _exitItem
        ]);

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _icons[_state],
            Text = TrayTextFormatter.Format(0, "0,00 MH/s", "— °C"),
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        SetLanguage("fr");
    }

    public event Action? OpenRequested;
    public event Action? StartRequested;
    public event Action? StopRequested;
    public event Action? ExitRequested;

    public System.Windows.Media.ImageSource WindowIcon
    {
        get
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(_icons[TrayIconState.Mining].Handle,
                Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            return source;
        }
    }

    public void SetLanguage(string? language)
    {
        var normalized = language?.Equals("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "fr";
        if (_language == normalized) return;
        _language = normalized;
        var english = normalized == "en";
        _openItem.Text = english ? "Open" : "Ouvrir";
        _startItem.Text = english ? "Start" : "Démarrer";
        _stopItem.Text = english ? "Stop" : "Arrêter";
        _exitItem.Text = english ? "Exit" : "Quitter";
    }

    public void Update(TrayIconState state, int selectedGpuCount, string? hashrate, string? temperature,
        bool canStart, bool canStop)
    {
        if (_disposed) return;
        if (_state != state)
        {
            _state = state;
            _notifyIcon.Icon = _icons[state];
        }
        _notifyIcon.Text = TrayTextFormatter.Format(selectedGpuCount, hashrate, temperature);
        _startItem.Enabled = canStart;
        _stopItem.Enabled = canStop;
    }

    public void ShowWarning(string title, string message)
    {
        if (_disposed) return;
        _notifyIcon.ShowBalloonTip(10_000, title, message, WinForms.ToolTipIcon.Warning);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        foreach (var icon in _icons.Values) icon.Dispose();
    }

    private static Icon CreateIcon(Color accent)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(5, 14, 8));
            using var border = new Pen(accent, 2.4f);
            graphics.FillEllipse(background, 2, 2, 28, 28);
            graphics.DrawEllipse(border, 2.5f, 2.5f, 27, 27);
            using var font = new Font("Segoe UI", 17, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(accent);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("K", font, brush, new RectangleF(2, 1, 28, 29), format);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
