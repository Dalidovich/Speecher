using Speecher.App;

namespace Speecher;

public class Program
{
    public static int Main()
    {
        using var app = new SpeecherApp();
        return app.Run();
    }
}
