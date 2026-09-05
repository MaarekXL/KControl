using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeryxControl.Models;
using KeryxControl.Services;
using KeryxControl.ViewModels;

namespace KeryxControl;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly TrayIconService _tray = new();
    private bool _initialized, _allowClose, _shutdownInProgress;
    private bool _logScrollPending;
    private WindowState _restoreWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();
        Icon = _tray.WindowIcon;
        DataContext = _vm;
        Loaded += Window_Loaded;
        StateChanged += Window_StateChanged;
        Closed += Window_Closed;
        _vm.Logs.CollectionChanged += Logs_CollectionChanged;
        _vm.Gpus.CollectionChanged += Gpus_CollectionChanged;
        _vm.PropertyChanged += ViewModel_PropertyChanged;
        _vm.StartCommand.CanExecuteChanged += Command_CanExecuteChanged;
        _vm.StopCommand.CanExecuteChanged += Command_CanExecuteChanged;
        _vm.MiningHealthAlertRaised += MiningHealthAlert_Raised;
        _tray.OpenRequested += RestoreFromTray;
        _tray.StartRequested += Tray_StartRequested;
        _tray.StopRequested += Tray_StopRequested;
        _tray.ExitRequested += Tray_ExitRequested;
        UpdateTray();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.InitializeAsync();
            ApplySavedWindowPlacement();
            LanguageCombo.SelectedIndex = _vm.Language == "en" ? 1 : 0;
            _initialized = true;
            _vm.SetLanguage(_vm.Language);
            UpdateTray();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Keryx Control", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplySavedWindowPlacement()
    {
        Width = Math.Max(MinWidth, _vm.SavedWindowWidth);
        Height = Math.Max(MinHeight, _vm.SavedWindowHeight);
        if (_vm.SavedWindowLeft is double left && _vm.SavedWindowTop is double top
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100
            && left + Width > SystemParameters.VirtualScreenLeft + 100
            && top + Height > SystemParameters.VirtualScreenTop + 100)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left; Top = top;
        }
        if (_vm.SavedWindowMaximized) WindowState = WindowState.Maximized;
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || sender is not ComboBox { SelectedItem: ComboBoxItem item } || item.Tag is not string language) return;
        _vm.SetLanguage(language);
    }

    private void OpenEscrow_Click(object sender, RoutedEventArgs e) => new EscrowWindow { Owner = this, DataContext = _vm }.ShowDialog();

    private void LogList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0 && !_vm.IsLogPaused) _vm.PauseLog();
    }

    private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm.IsLogPaused || _vm.Logs.Count == 0 || _logScrollPending) return;
        _logScrollPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _logScrollPending = false;
            if (!_vm.IsLogPaused && _vm.Logs.Count > 0) LogList.ScrollIntoView(_vm.Logs[^1]);
        });
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            return;
        }
        _restoreWindowState = WindowState;
    }

    public void RestoreFromTray()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(RestoreFromTray);
            return;
        }
        if (_shutdownInProgress) return;
        Show();
        WindowState = _restoreWindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void Tray_StartRequested()
    {
        if (_vm.StartCommand.CanExecute(null)) _vm.StartCommand.Execute(null);
    }

    private void Tray_StopRequested()
    {
        if (_vm.StopCommand.CanExecute(null)) _vm.StopCommand.Execute(null);
    }

    private void Tray_ExitRequested()
    {
        RestoreFromTray();
        Close();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateTray();
    private void Gpus_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateTray();
    private void Command_CanExecuteChanged(object? sender, EventArgs e) => UpdateTray();

    private void UpdateTray()
    {
        _tray.SetLanguage(_vm.Language);
        _tray.Update(_vm.TrayState, _vm.SelectedGpuCount, _vm.Hashrate, _vm.Temperature,
            _vm.StartCommand.CanExecute(null), _vm.StopCommand.CanExecute(null));
    }

    private void MiningHealthAlert_Raised(MiningHealthAlert alert)
    {
        var title = Application.Current.TryFindResource("TrayAlertTitle") as string ?? "Keryx Control";
        var key = alert.Kind == MiningHealthAlertKind.ZeroHashrate ? "TrayZeroHashrate" : "TrayHighTemperature";
        var template = Application.Current.TryFindResource(key) as string ?? key;
        var message = alert.Value is double value ? string.Format(template, value) : template;
        _tray.ShowWarning(title, message);
    }

    private void LogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_vm.IsLogPaused || e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0) return;
        if (e.VerticalOffset < e.ExtentHeight - e.ViewportHeight - 1) _vm.PauseLog();
    }

    private void ResumeLog_Click(object sender, RoutedEventArgs e)
    {
        _vm.ResumeLog();
        if (_vm.Logs.Count > 0) LogList.ScrollIntoView(_vm.Logs[^1]);
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = _vm.GetLogText();
            if (!string.IsNullOrWhiteSpace(text)) Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Keryx Control", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_shutdownInProgress) return;
        if (_vm.HasActiveProcesses)
        {
            var answer = MessageBox.Show(
                Application.Current.TryFindResource("ExitConfirmation") as string ?? "Stop Keryx processes and exit?",
                Application.Current.TryFindResource("ExitTitle") as string ?? "Keryx Control",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }
        _shutdownInProgress = true;
        IsEnabled = false;
        var restoreBounds = RestoreBounds;
        _vm.SetWindowPlacement(restoreBounds.Width, restoreBounds.Height, restoreBounds.Left, restoreBounds.Top, WindowState == WindowState.Maximized);
        try { await _vm.ShutdownAsync(); }
        finally { _allowClose = true; Close(); }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _vm.Logs.CollectionChanged -= Logs_CollectionChanged;
        _vm.Gpus.CollectionChanged -= Gpus_CollectionChanged;
        _vm.PropertyChanged -= ViewModel_PropertyChanged;
        _vm.StartCommand.CanExecuteChanged -= Command_CanExecuteChanged;
        _vm.StopCommand.CanExecuteChanged -= Command_CanExecuteChanged;
        _vm.MiningHealthAlertRaised -= MiningHealthAlert_Raised;
        _tray.Dispose();
    }
}
