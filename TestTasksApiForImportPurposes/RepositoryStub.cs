namespace TestTasksApiForImportPurposes;

public class RepositoryStub
{
    private Dictionary<Guid, SubTasksCounter> subTasksCounters = new();

    private object completeManagerTaskLocker = new object();
    private int iterationsCounter = 0;
    private int wholeBatchSize;
    private int callsCounter; // need to bind to value of callersTaskCompletionSource
    private int completedTasksCounter; // need to bind to value of callersTaskCompletionSource
    private int waitingForSubTasksCounter; // need to bind to value of callersTaskCompletionSource
    private (TaskCompletionSource<int>, Guid)? callersTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());
    private (TaskCompletionSource<int>, Guid)? managerTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());

    public void EnterBatchMode(int batchSize)
    {
        this.wholeBatchSize = batchSize;
        this.callsCounter = 0;
        this.completedTasksCounter = 0;
        this.waitingForSubTasksCounter = 0;
        this.managerTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());
        this.callersTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());
        this.subTasksCounters = new Dictionary<Guid, SubTasksCounter>();

        ConsoleLogger.WriteLine($"manager source is {this.managerTaskCompletionSource.Value.Item2}; callers source is {this.callersTaskCompletionSource.Value.Item2}");
    }

    public async Task<bool> WaitWhenReadyForNextStepAsync()
    {
        if (this.completedTasksCounter < this.wholeBatchSize)
        {
            ConsoleLogger.WriteLine($"going to wait for manager source {this.managerTaskCompletionSource.Value.Item2}");

            await this.managerTaskCompletionSource.Value.Item1.Task;
            return false;
        }

        return true;
    }

    public void TaskCompleted(Guid guid)
    {
        var incrementedValue= Interlocked.Increment(ref this.completedTasksCounter);
        ConsoleLogger.WriteLine($"task completed {incrementedValue} time", guid);

        this.CompleteManagerTaskIfAllCallsAreStopped(guid);
    }

    private void SubTaskCompleted(Guid subTaskGuid, Guid taskGuid)
    {
        var decrementedCount = this.subTasksCounters[taskGuid].Decrement();
        ConsoleLogger.WriteLine($"completing sub-task of {subTaskGuid} and now there is {decrementedCount} tasks left to wait", taskGuid);
        if (decrementedCount == 0)
        {
            var decrementedWaitingForSubTasks = Interlocked.Decrement(ref this.waitingForSubTasksCounter);
            ConsoleLogger.WriteLine($"all sub-tasks are completed, so goes to {decrementedWaitingForSubTasks} of waiting tasks", taskGuid);
        }

        this.TaskCompleted(subTaskGuid);
    }

    private void CompleteManagerTaskIfAllCallsAreStopped(Guid guid)
    {
        // order of "snapshots" is important here
        int awaitingCallsDetected = this.callsCounter;
        int tasksCompleted = this.completedTasksCounter;
        int waitingForSubTasks = this.waitingForSubTasksCounter;
        int batchSize = this.wholeBatchSize;

        ConsoleLogger.WriteLine($"trying to complete when {tasksCompleted} tasks completed and {awaitingCallsDetected} tasks awaiting and {waitingForSubTasks} awaiting for sub-tasks [compared to {batchSize}]", guid);

        // todo: need to change to strict equal (no there is a flaw somewhere)
        if (awaitingCallsDetected + tasksCompleted + waitingForSubTasks == batchSize)
        {
            var iterationsCounterBeforeLock = iterationsCounter;
            lock (completeManagerTaskLocker)
            {
                if (iterationsCounterBeforeLock != iterationsCounter)
                {
                    ConsoleLogger.WriteLine($"iteration is already completed", guid);
                }
                else
                {
                    ConsoleLogger.WriteLine($"completing manager task {this.managerTaskCompletionSource.Value.Item2} as all is fine", guid);
                    this.managerTaskCompletionSource.Value.Item1.TrySetResult(wholeBatchSize);

                    iterationsCounter++;
                }
            }
        }
    }

    public void DoBatchStep()
    {
        Interlocked.Exchange(ref this.callsCounter, 0);
        ConsoleLogger.WriteLine($"did the batch step ({this.completedTasksCounter} tasks completed)");

        // managers flow should be released first, then callers flow
        // release managers flow
        var previousManagerTask = this.managerTaskCompletionSource;
        this.managerTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());

        // release callers flow
        var previousCallersTask = this.callersTaskCompletionSource;
        this.callersTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());
        // can be "AlreadyCompletedException" which should be processed
        previousCallersTask.Value.Item1.TrySetResult(10);

        ConsoleLogger.WriteLine($"finished both sources: manager was {previousManagerTask.Value.Item2}; new one is {this.managerTaskCompletionSource.Value.Item2}" +
                                $" | callers was {previousCallersTask.Value.Item2}; new one is {this.callersTaskCompletionSource.Value.Item2};");
    }

    public void ExitBatchMode()
    {
        Interlocked.Exchange(ref this.callsCounter, 0);
        Interlocked.Exchange(ref this.completedTasksCounter, 0);

        this.callersTaskCompletionSource = null;
        this.managerTaskCompletionSource = null;
    }

    public async Task<int> DoSomethingAsync(int i, Guid guid)
    {
        // order of calls is important in here!
        var callersTaskToWait = this.callersTaskCompletionSource.Value;
        var incrementedValue = Interlocked.Increment(ref this.callsCounter);
        ConsoleLogger.WriteLine($"DoSomethingAsync is called {incrementedValue} time (for callers task {callersTaskToWait.Item2})", guid);

        this.CompleteManagerTaskIfAllCallsAreStopped(guid);

        ConsoleLogger.WriteLine($"going to wait for callers source {callersTaskToWait.Item2}", guid);
        await callersTaskToWait.Item1.Task;
        return i;
    }

    public async Task WhenAllRepositoryAwareTasksAsync(Guid guid, int tasksCount, Func<IEnumerable<(Task, Guid)>> tasksGetter)
    {
        var newBatchSize = Interlocked.Add(ref this.wholeBatchSize, tasksCount);
        var waitingForSubTasksIncreased = Interlocked.Increment(ref this.waitingForSubTasksCounter);
        this.subTasksCounters.Add(guid, new SubTasksCounter(tasksCount));
        ConsoleLogger.WriteLine($"spin off {tasksCount} (total batch size increased up to {newBatchSize}; waiting for sub-tasks increased up to {waitingForSubTasksIncreased})", guid);

        var tasksWithCompletion = tasksGetter()
            .Select((taskWithGuid) =>
            {
                // it is better to bind parent task and its sub-tasks to reduce waitingForSubTasksCounter before last sub-task completion
                ConsoleLogger.WriteLine($"generated sub-task {taskWithGuid.Item2}", guid);
                return taskWithGuid.Item1.ContinueWith((_) => this.SubTaskCompleted(taskWithGuid.Item2, guid));
            })
            .ToList();

        if (tasksWithCompletion.Count() != tasksCount)
        {
            throw new ArgumentOutOfRangeException("tasksCount and count of tasks returned by tasksGetter should be equal");
        }

        await Task.WhenAll(tasksWithCompletion);
    }

    private class SubTasksCounter
    {
        private int countOfActiveSubTasks;

        public SubTasksCounter(int initialCount)
        {
            this.countOfActiveSubTasks = initialCount;
        }

        public int Decrement()
        {
            return Interlocked.Decrement(ref this.countOfActiveSubTasks);
        }
    }
}