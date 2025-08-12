using PropertyChanged;
using System;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Windows.Input;
using System.Windows.Threading;

namespace DuplicateFinder
{
    [AddINotifyPropertyChangedInterface]
    public abstract class VmBase : INotifyPropertyChanged
    {
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        protected IScheduler Scheduler { get; } = DispatcherScheduler.Current;

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler CloseRequested;

        public virtual void OnPropertyChanged(string propertyName)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged != null)
            {
                propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public virtual void OnLoad() { }
        public virtual void OnClose() { }

        protected void RequestClose()
        {
            if (CloseRequested != null)
                _dispatcher.Invoke(() => CloseRequested.Invoke(this, EventArgs.Empty));
        }
    }

    public class Command : ICommand
    {
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public Command(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
    
        public void Execute(object parameter) => _execute(parameter);

        public void RaiseCanExecute()
        {
            if (CanExecuteChanged != null)
                _dispatcher.Invoke(() => CanExecuteChanged(this, EventArgs.Empty));
        }
    }
}