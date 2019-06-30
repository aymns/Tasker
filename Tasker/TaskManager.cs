using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tasker
{
    /// <summary>
    ///     Represent task manager used to control number of threads to execute async functions
    /// </summary>
    public class TaskManager : ITaskManager
    {
        /// <summary>
        ///     A delegate to be called when exception happened.
        /// </summary>
        /// <param name="e"></param>
        public delegate void HandleException(Exception e);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Queue<Task> _currentRunningTasks = new Queue<Task>();
        private readonly HandleException _exceptionHandler;
        private readonly object _locker = new object();
        public int MaxThreads { get; private set; } = 100;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(true);
        private bool _abortAllCalled;
        private bool _isDisposed;

        /// <summary>
        /// Initialize new instance of task manager, if <param name="maxThreads"></param> is 1 then the task manager will work in a sync way
        /// means there will be one thread
        /// </summary>
        /// <param name="maxThreads">max number of threads the task manager can create.</param>
        /// <param name="exceptionHandler">delegate function to be called when exception occured.</param>
        public TaskManager(int maxThreads, HandleException exceptionHandler)
        {
            if (MaxThreads < 1)
                throw new InvalidOperationException("Number of max threads has to be greater than 1");

            MaxThreads = maxThreads;
            _exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// Initialize new instance of task manager, if <param name="maxThreads"></param> is 1 then the task manager will work in a sync way
        /// means there will be one thread
        /// </summary>
        /// <param name="maxThreads">max number of threads the task manager can create.</param>
        public TaskManager(int maxThreads) : this(maxThreads, null)
        {
        }


        /// <inheritdoc />
        public int NumberOfRunningThreads { private set; get; }

        public void Dispose()
        {
            if (!_abortAllCalled)
                WaitForAllTasksToFinish();

            _isDisposed = true;
            _cancellationTokenSource.Cancel();
            _resetEvent.Dispose();
            AbortAll();
        }

        /// <inheritdoc />
        public void DoWork(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (_abortAllCalled)
                throw new InvalidOperationException(
                    "Cannot call DoWork() after AbortAll() or Dispose() have been called");

            if (!_isDisposed && MaxThreads > 1)
            {
                _resetEvent.WaitOne();
                lock (_locker)
                {
                    NumberOfRunningThreads++;
                    if (!_isDisposed && NumberOfRunningThreads >= MaxThreads)
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

        /// <inheritdoc />
        public void AbortAll()
        {
            _abortAllCalled = true;
            lock (_locker)
            {
                NumberOfRunningThreads = 0;
            }

            _cancellationTokenSource.Cancel();
        }


        /// <summary>
        /// This method used before disposing task manager, more specifically to wait the last task to finish.
        /// </summary>
        private void WaitForAllTasksToFinish()
        {
            if (!_currentRunningTasks.Any())
                return;

            Task.WaitAll(_currentRunningTasks.ToArray());
        }

        /// <summary>
        /// start <param name="action"></param>,
        /// when it finishes do the following
        ///     - If <see cref="decrementRunningThreadCountOnCompletion"/> decrease number of running threads <see cref="NumberOfRunningThreads"/>
        ///     - If <see cref="CanCreateNewThread"/> is true then send a manual reset event, to release block on starting new threads
        /// </summary>
        /// <param name="action"></param>
        /// <param name="decrementRunningThreadCountOnCompletion"></param>
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
                        _currentRunningTasks.Dequeue();
                        NumberOfRunningThreads--;
                        if (CanCreateNewThread)
                            _resetEvent.Set();
                    }
            }
        }

        private bool CanCreateNewThread => !_isDisposed && NumberOfRunningThreads < MaxThreads;

        /// <summary>
        /// Run action on new thread.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        private Task RunActionOnDedicatedThread(Action action)
        {
            var task = Task.Factory
                .StartNew(() => RunAction(action), _cancellationTokenSource.Token);

            task.ContinueWith(HandleAggregateExceptions, TaskContinuationOptions.OnlyOnFaulted);

            return task;
        }

        /// <summary>
        /// Handle task exceptions
        /// </summary>
        /// <param name="task"></param>
        private void HandleAggregateExceptions(Task task)
        {
            if (task?.Exception == null || _exceptionHandler == null)
                return;

            var aggException = task.Exception.Flatten();
            foreach (var exception in aggException.InnerExceptions) _exceptionHandler.Invoke(exception);
        }
    }
}