// See https://aka.ms/new-console-template for more information

using TestTasksApiForImportPurposes;

var repository = new RepositoryStub();

const int noOperationTasksCount = 10;
const int simpleTasksCount = 1_000;
const int complexTasksCount = 1_000;
const int superComplexTasksCount = 1_000;
const int totalTasksCount = noOperationTasksCount + simpleTasksCount + complexTasksCount + superComplexTasksCount;
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
    ConsoleLogger.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: --- STEP {i}: tasks completed count = {tasksToWait.Count(t => t.IsCompleted)}");

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