namespace TestTasksApiForImportPurposes;

public class RepositoryStub
{
    private object completeManagerTaskLocker = new object();
    private int iterationsCounter = 0;
    private int batchSize;
    private int callsCounter; // need to bind to value of callersTaskCompletionSource
    private int completedTasksCounter; // need to bind to value of callersTaskCompletionSource
    private (TaskCompletionSource<int>, Guid)? callersTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());
    private (TaskCompletionSource<int>, Guid)? managerTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());

    public void EnterBatchMode(int batchSize)
    {
        this.batchSize = batchSize;
        this.callsCounter = 0;
        this.completedTasksCounter = 0;
        this.managerTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());
        this.callersTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());

        ConsoleLogger.WriteLine($"manager source is {this.managerTaskCompletionSource.Value.Item2}; callers source is {this.callersTaskCompletionSource.Value.Item2}");
    }

    public async Task<bool> WaitWhenReadyForNextStepAsync()
    {
        if (this.completedTasksCounter < this.batchSize)
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

    private void CompleteManagerTaskIfAllCallsAreStopped(Guid guid)
    {
        int awaitingCallsDetected = this.callsCounter;
        int tasksCompleted = this.completedTasksCounter;

        ConsoleLogger.WriteLine($"trying to complete when {tasksCompleted} tasks completed and {awaitingCallsDetected} tasks awaiting", guid);

        if (awaitingCallsDetected + tasksCompleted >= batchSize)
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
                    ConsoleLogger.WriteLine($"completing manager task as all is fine", guid);
                    this.managerTaskCompletionSource.Value.Item1.TrySetResult(batchSize);

                    iterationsCounter++;
                }
            }
        }
    }

    public void DoBatchStep()
    {
        Interlocked.Exchange(ref this.callsCounter, 0);
        ConsoleLogger.WriteLine($"did the batch step ({this.completedTasksCounter} tasks completed)");

        // release callers flow
        var previousCallersTask = this.callersTaskCompletionSource;
        this.callersTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());
        // can be "AlreadyCompletedException" which should be processed
        previousCallersTask.Value.Item1.TrySetResult(10);

        // release managers flow
        var previousManagerTask = this.managerTaskCompletionSource;
        this.managerTaskCompletionSource = (new TaskCompletionSource<int>(), Guid.NewGuid());

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
        var incrementedValue = Interlocked.Increment(ref this.callsCounter);
        ConsoleLogger.WriteLine($"DoSomethingAsync is called {incrementedValue} time", guid);

        var callersTaskToWait = this.callersTaskCompletionSource.Value;
        this.CompleteManagerTaskIfAllCallsAreStopped(guid);

        ConsoleLogger.WriteLine($"going to wait for callers source {callersTaskToWait.Item2}", guid);
        await callersTaskToWait.Item1.Task;
        return i;
    }
}