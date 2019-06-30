using System;

namespace Tasker
{
    public interface ITaskManager : IDisposable
    {
        /// <summary>
        /// Run the <param name="action"></param> in asynchronously separate thread if <see cref="TaskManager"/> is not not busy or wait
        /// until there is a free thread.
        /// </summary>
        /// <param name="action"></param>
        void DoWork(Action action);

        /// <summary>
        /// Cancel all unfinished tasks.
        /// </summary>
        void AbortAll();


        /// <summary>
        /// Get number of current running threads.
        /// </summary>
        int NumberOfRunningThreads { get; }
    }
}
