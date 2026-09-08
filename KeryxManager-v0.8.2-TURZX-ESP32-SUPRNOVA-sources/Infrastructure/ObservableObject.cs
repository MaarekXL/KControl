using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace KeryxControl.Infrastructure;
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; PropertyChanged?.Invoke(this, new(name)); return true; }
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _busy;
    public bool CanExecute(object? parameter) => !_busy && (canExecute?.Invoke() ?? true);
    public event EventHandler? CanExecuteChanged;
    public event Action<Exception>? Failed;
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _busy = true;
        Raise();
        try { await execute(); }
        catch (Exception ex) { Failed?.Invoke(ex); }
        finally { _busy = false; Raise(); }
    }
    public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
