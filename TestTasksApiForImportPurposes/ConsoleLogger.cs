using System.Xml;

namespace TestTasksApiForImportPurposes;

public static class ConsoleLogger
{
    public static void WriteLine(string message, Guid? guid = null)
    {
        // Console.WriteLine($"[{guid?.ToString() ?? ""}]-[{DateTime.Now:HH:mm:ss.fff}-{Thread.CurrentThread.ManagedThreadId}] {message}");
    }
}