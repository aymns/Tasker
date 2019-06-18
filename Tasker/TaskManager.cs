using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tasker
{
    public class TaskManager : IDisposable
    {
        public delegate void HandleException(Exception e);

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly HandleException _exceptionHandler;
        private readonly object _locker = new object();
        private readonly int _maxThreads = 100;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(true);
        private bool _abortAllCalled;
        private bool _isDisposed;

        Queue<Task> _currentRunningTasks = new Queue<Task>();

        public TaskManager(int maxThreads, HandleException exceptionHandler)
        {
            if (_maxThreads < 1)
                throw new InvalidOperationException("Number of max threads has to be greater than 1");

            _maxThreads = maxThreads;
            _exceptionHandler = exceptionHandler;
        }

        public TaskManager(int maxThreads) : this(maxThreads, null)
        {
        }

        public int NumberOfRunningThreads { private set; get; }

        public void Dispose()
        {
            if (!_abortAllCalled)
                WaitForTheLastTaskToFinish();

            _isDisposed = true;
            _cancellationTokenSource.Cancel();
            _resetEvent.Dispose();
            AbortAll();
        }
        
        private void WaitForTheLastTaskToFinish()
        {
            if (!_currentRunningTasks.Any())
            {
                return;
            }

            Task.WaitAll(this._currentRunningTasks.ToArray());
        }

        public void DoWork(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (_abortAllCalled)
                throw new InvalidOperationException(
                    "Cannot call DoWork() after AbortAll() or Dispose() have been called");

            if (!_isDisposed && _maxThreads > 1)
            {
                _resetEvent.WaitOne();
                lock (_locker)
                {
                    NumberOfRunningThreads++;
                    if (!_isDisposed && NumberOfRunningThreads >= _maxThreads)
                        _resetEvent.Reset();
                }

                var task = RunActionOnDedicatedThread(action);
                _currentRunningTasks.Enqueue(task);
            }
            else
            {
                RunAction(action, false);
            }
        }

        public void AbortAll()
        {
            _abortAllCalled = true;
            lock (_locker)
            {
                NumberOfRunningThreads = 0;
            }

            _cancellationTokenSource.Cancel();
        }

        private void RunAction(Action action, bool decrementRunningThreadCountOnCompletion = true)
        {
            try
            {
                action.Invoke();
            }
            finally
            {
                if (decrementRunningThreadCountOnCompletion)
                    lock (_locker)
                    {
                        this._currentRunningTasks.Dequeue();
                        NumberOfRunningThreads--;
                        if (!_isDisposed && NumberOfRunningThreads < _maxThreads)
                            _resetEvent.Set();
                    }
            }
        }

        private Task RunActionOnDedicatedThread(Action action)
        {
            var task = Task.Factory
                .StartNew(() => RunAction(action), _cancellationTokenSource.Token);

            task.ContinueWith(HandleAggregateExceptions, TaskContinuationOptions.OnlyOnFaulted);

            return task;
        }

        private void HandleAggregateExceptions(Task task)
        {
            if (task?.Exception == null || _exceptionHandler == null)
                return;

            var aggException = task.Exception.Flatten();
            foreach (var exception in aggException.InnerExceptions) _exceptionHandler.Invoke(exception);
        }
    }
}