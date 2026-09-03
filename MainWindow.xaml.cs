using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeryxControl.ViewModels;

namespace KeryxControl;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private bool _initialized, _allowClose, _shutdownInProgress;
    private bool _logScrollPending;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += Window_Loaded;
        _vm.Logs.CollectionChanged += Logs_CollectionChanged;
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
}
