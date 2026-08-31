using System.IO;
using System.Windows;
using System.Windows.Threading;
namespace KeryxControl;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
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
