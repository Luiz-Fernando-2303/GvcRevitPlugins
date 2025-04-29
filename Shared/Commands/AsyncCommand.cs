using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GvcRevitPlugins.Shared.Commands
{
    public abstract class AsyncCommand : ICommand
    {
        private static bool _isAnyExecuting;
        private bool _isExecuting;
        public static bool IsAnyExecuting
        {
            get => _isAnyExecuting;
            set => _isAnyExecuting = value;
        }
        public bool IsExecuting
        {
            get
            {
                return _isExecuting;
            }
            set
            {
                _isExecuting = value;
                _isAnyExecuting = value;
                OnCanExecuteChanged();
            }
        }

        public event EventHandler CanExecuteChanged;

        public virtual bool CanExecute(object parameter) => !_isAnyExecuting;

        public async void Execute(object parameter)
        {
            IsExecuting = true;

            await ExecuteAsync(parameter);

            IsExecuting = false;
        }

        public abstract Task ExecuteAsync(object parameter);

        protected void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, new EventArgs());
        }
    }
}
