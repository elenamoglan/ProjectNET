using VoidRunner.App;

namespace VoidRunner;

internal static class Program
{
    private static void Main()
    {
        using var app = new GameApplication();
        app.Run();
    }
}
