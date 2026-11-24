using System.Windows.Input;

namespace EcoKoin.ViewModels
{
    /// <summary>
    /// ICommand over an async delegate. While one execution is running the command cannot execute again
    /// (no re-entrancy). Failures are never swallowed: they go to <c>onError</c> when given, otherwise they are
    /// rethrown on the caller's synchronization context (WPF: the dispatcher's unhandled-exception path).
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private readonly Action<Exception>? _onError;
        private bool _isRunning;

        public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null, Action<Exception>? onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
        }

        public event EventHandler? CanExecuteChanged;

        /// <summary>True while an execution is in flight.</summary>
        public bool IsRunning => _isRunning;

        public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(parameter) ?? true);

        /// <summary>ICommand entry point (fire and forget, errors handled as described on the class).</summary>
        public async void Execute(object? parameter)
        {
            try
            {
                await ExecuteAsync(parameter);
            }
            catch (Exception ex) when (_onError is not null)
            {
                _onError(ex);
            }
        }

        /// <summary>Awaitable execution. Returns immediately if the command cannot execute. Exceptions propagate to the caller.</summary>
        public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isRunning = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute(parameter);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
