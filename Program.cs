using VoidRunner.App;

namespace VoidRunner;

internal static class Program
{
    private static void Main()
    {
        try
        {
            using var app = new GameApplication();
            app.Run();
        }
        catch (Exception ex)
        {
            TryWriteCrashLog(ex);
            throw;
        }
    }

    private static void TryWriteCrashLog(Exception ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoidRunner");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "crash.log");

            string content =
                $"[{DateTimeOffset.Now:O}] Unhandled exception:{Environment.NewLine}{ex}{Environment.NewLine}";
            File.AppendAllText(path, content);
        }
        catch
        {
        }
    }
}
