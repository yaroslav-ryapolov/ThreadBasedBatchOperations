namespace TestTasksApiForImportPurposes;

public class ServiceStub
{
    public static async Task<bool> NoRepositoryCallCreateObjectStub(RepositoryStub repository, int i, Guid guid)
    {
        await Task.Delay(i * 1_000);
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
        var bunchOfTasks = Enumerable.Range(1, 10).Select(j => repository.DoSomethingAsync(j * i, guid))
            .ToList();

        await Task.WhenAll(bunchOfTasks.Cast<Task>().ToArray());

        return bunchOfTasks.All(t => t is { IsCompleted: true, Result: > 0 });
    }
}