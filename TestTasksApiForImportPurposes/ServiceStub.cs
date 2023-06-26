namespace TestTasksApiForImportPurposes;

public class ServiceStub
{
    public static async Task<bool> NoRepositoryCallCreateObjectStub(RepositoryStub repository, int i, Guid guid)
    {
        // await Task.Delay(i * 10);
        return i % 2 == 0;
    }

    public static async Task<bool> CreateObjectStub(RepositoryStub repository, int i, Guid guid)
    {
        var result = await repository.DoSomethingAsync(i, guid);
        return result > 0;
    }

    public static async Task<bool> ComplexWithSingleThreadCreateObjectStub(RepositoryStub repository, int i, Guid guid)
    {
        foreach (var j in Enumerable.Range(1, 10))
        {
            ConsoleLogger.WriteLine($"doing iteration {j}", guid);
            await repository.DoSomethingAsync(j * i, guid);
            ConsoleLogger.WriteLine($"did iteration {j}", guid);
        }

        return true;
    }

    public static async Task<bool> ComplexWithMultipleThreadsCreateObjectStub(RepositoryStub repository, int i, Guid guid)
    {
        const int TaskCount = 3;

        List<Task<int>>? bunchOfTasks = null;
        await repository.WhenAllRepositoryAwareTasksAsync(
            guid,
            TaskCount,
            () =>
            {
                var result = Enumerable.Range(1, TaskCount)
                    .Select(j =>
                    {
                        var subTaskGuid = Guid.NewGuid();


                        return (repository.DoSomethingAsync(j * i, subTaskGuid), subTaskGuid);
                    })
                    .ToList();

                bunchOfTasks = result.Select(x => x.Item1).ToList();
                return result.Select(x => ((Task)x.Item1, x.Item2));
            });

        return bunchOfTasks.All(t => t is { IsCompleted: true, Result: > 0 });
    }
}