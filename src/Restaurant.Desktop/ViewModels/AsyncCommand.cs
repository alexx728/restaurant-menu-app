using System.Windows.Input;

namespace Restaurant.Desktop.ViewModels;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _action;
    private readonly Func<bool> _canExecute;
    private readonly Action<Exception> _onError;
    private bool _running;

    public AsyncCommand(Func<Task> action, Func<bool> canExecute, Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(canExecute);
        ArgumentNullException.ThrowIfNull(onError);
        _action = action;
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_running && _canExecute();

    public async void Execute(object? parameter) => await ExecuteAsync();

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
            return;

        _running = true;
        Refresh();
        try
        {
            await _action();
        }
        catch (Exception error)
        {
            _onError(error);
        }
        finally
        {
            _running = false;
            Refresh();
        }
    }

    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
