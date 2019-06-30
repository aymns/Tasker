# Tasker

Simple task manager, to control the maximum number of tasks that can be run asynchronously. 


## Getting Started

To install the package from nuget.org

`Install-Package Tasker`

### Code sample

```
    // Create new task manager instance that can run 2 function asynchronously on 2 different threads at maximum.
    var taskManager = new TaskManager(2, HandleException);
    using (taskManager)
    {
        taskManager.DoWork(() => HeavyProcess(1, 20000));
        taskManager.DoWork(() => HeavyProcess(2, 12000));
        taskManager.DoWork(() => HeavyProcess(3, 5000, true));
        taskManager.DoWork(() => HeavyProcess(4, 12000));
        taskManager.DoWork(() => HeavyProcess(5, 5000));
    }

    // Handle exception
    private void HandleException(Exception exception)
    {
        Console.WriteLine(exception.Message);
    }

```