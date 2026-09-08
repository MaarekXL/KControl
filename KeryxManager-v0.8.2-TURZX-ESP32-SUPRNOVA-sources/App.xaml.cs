using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
namespace KeryxControl;
public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\KeryxControl.Manager.Instance";
    private const string ActivationEventName = @"Local\KeryxControl.Manager.Activate";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;
    private Task? _activationListener;
    private bool _ownsInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _instanceMutex = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        _ownsInstanceMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            _activationEvent.Set();
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        StartActivationListener();
    }

    private void StartActivationListener()
    {
        if (_activationEvent is null) return;
        _activationCancellation = new CancellationTokenSource();
        var cancellation = _activationCancellation;
        var activationEvent = _activationEvent;
        _activationListener = Task.Run(() =>
        {
            var handles = new WaitHandle[] { activationEvent, cancellation.Token.WaitHandle };
            while (!cancellation.IsCancellationRequested)
            {
                if (WaitHandle.WaitAny(handles) != 0 || cancellation.IsCancellationRequested) break;
                if (Dispatcher.HasShutdownStarted) break;
                _ = Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is MainWindow window) window.RestoreFromTray();
                });
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        try { _activationEvent?.Set(); } catch { }
        try { _activationListener?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _activationCancellation?.Dispose();
        _activationEvent?.Dispose();
        if (_ownsInstanceMutex)
        {
            try { _instanceMutex?.ReleaseMutex(); } catch { }
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeryxControl");
        var path = Path.Combine(folder, "crash.log");
        try
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(path, $"[{DateTime.Now:O}]{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }
        try
        {
            MessageBox.Show(
                $"Keryx Control a rencontré une erreur et doit se fermer. / Keryx Control encountered an error and must close.{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}{Environment.NewLine}{Environment.NewLine}Crash log: {path}",
                "Keryx Control", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
        e.Handled = true;
        Shutdown(-1);
    }
}
