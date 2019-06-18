using System;
using System.Threading;
using Xunit;

namespace Tasker.Tests
{
    public class TaskManagerTests
    {
        private int _data = 3;
        private int _errorsCount = 0;
        private int _completedTasksCount = 0;

        [Fact]
        public void MultipleTasksAreAdded_ProcessAllTasks_AllTaskAreProcessedSuccessfully()
        {
            var taskManager = new TaskManager(2, HandleException);
            using (taskManager)
            {
                taskManager.DoWork(() => HeavyProcess(1, 20000));
                taskManager.DoWork(() => HeavyProcess(2, 12000));
                taskManager.DoWork(() => HeavyProcess(3, 5000));
                taskManager.DoWork(() => HeavyProcess(4, 12000));
                taskManager.DoWork(() => HeavyProcess(5, 5000));
            }

            Assert.True(taskManager.NumberOfRunningThreads == 0 && _completedTasksCount == 5);
        }

        [Fact]
        public void CorruptedTaskQueued_ProcessAllTasks_AllTaskAreProcessedAndExceptionHandled()
        {
            var taskManager = new TaskManager(2, HandleException);
            using (taskManager)
            {
                taskManager.DoWork(() => HeavyProcess(1, 20000));
                taskManager.DoWork(() => HeavyProcess(2, 12000));
                taskManager.DoWork(() => HeavyProcess(3, 5000, true));
                taskManager.DoWork(() => HeavyProcess(4, 12000));
                taskManager.DoWork(() => HeavyProcess(5, 5000));
            }

            Assert.True(_errorsCount == 1 && _completedTasksCount == 4);
        }

        private void HandleException(Exception exception)
        {
            _errorsCount++;
        }

        private void HeavyProcess(int id, int executionTime, bool throwException = false)
        {
            Thread.Sleep(executionTime);
            if (throwException)
                throw new Exception("Causing some problem here :)");

            _completedTasksCount++;
        }
    }
}