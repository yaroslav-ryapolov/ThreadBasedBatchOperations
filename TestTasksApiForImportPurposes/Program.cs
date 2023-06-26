// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using TestTasksApiForImportPurposes;

var timer = new Stopwatch();
timer.Start();

var repository = new RepositoryStub();

const int noOperationTasksCount = 100_000;
const int simpleTasksCount = 100_000;
const int complexTasksCount = 100_000;
const int superComplexTasksCount = 100_000;
const int totalTasksCount = noOperationTasksCount + simpleTasksCount
                                                  + complexTasksCount
                                                  + superComplexTasksCount
                                                  ;
repository.EnterBatchMode(totalTasksCount);

ConsoleLogger.WriteLine($"there are {totalTasksCount} tasks");

var noOperationTasks = Enumerable.Range(0, noOperationTasksCount)
    .Select(i =>
    {
        var guid = Guid.NewGuid();
        return ServiceStub.NoRepositoryCallCreateObjectStub(repository, i, guid)
            .ContinueWith((_) => repository.TaskCompleted(guid));
    });
var simpleTasks = Enumerable.Range(0, simpleTasksCount)
    .Select(i =>
    {
        var guid = Guid.NewGuid();
        return ServiceStub.CreateObjectStub(repository, i, guid)
            .ContinueWith((_) => repository.TaskCompleted(guid));
    });
var complexTasks = Enumerable.Range(0, complexTasksCount)
    .Select(i =>
    {
        var guid = Guid.NewGuid();
        return ServiceStub.ComplexWithSingleThreadCreateObjectStub(repository, i, guid)
            .ContinueWith((_) => repository.TaskCompleted(guid));
    });
var superComplexTasks = Enumerable.Range(0, superComplexTasksCount)
    .Select(i =>
    {
        var guid = Guid.NewGuid();
        return ServiceStub.ComplexWithMultipleThreadsCreateObjectStub(repository, i, guid)
            .ContinueWith((_) => repository.TaskCompleted(guid));
    });

var tasksToWait = noOperationTasks
    .Concat(simpleTasks)
    .Concat(complexTasks)
    .Concat(superComplexTasks)
    .ToList();

var allTasksTask = Task.WhenAll(tasksToWait);

// Better hide in ExitBatchMode or something like that
int i = 0;
bool needToWaitWithoutBatchSteps = false;
while (!needToWaitWithoutBatchSteps)
{
    ConsoleLogger.WriteLine($"+++ STEP {i}: tasks completed count = {tasksToWait.Count(t => t.IsCompleted)}");

    needToWaitWithoutBatchSteps = await repository.WaitWhenReadyForNextStepAsync();

    if (!needToWaitWithoutBatchSteps)
    {
        repository.DoBatchStep();
        i++;
    }
}

ConsoleLogger.WriteLine("HERE WE GO, going to just wait");
await allTasksTask;

repository.ExitBatchMode();

ConsoleLogger.WriteLine($"+++ LET'S CALL IT A DAY: tasks completed count = {tasksToWait.Count(t => t.IsCompleted):N0} (elapsed {timer.ElapsedMilliseconds:N0}ms)");
Console.WriteLine($"+++ LET'S CALL IT A DAY: tasks completed count = {tasksToWait.Count(t => t.IsCompleted):N0} (elapsed {timer.ElapsedMilliseconds:N0}ms)");