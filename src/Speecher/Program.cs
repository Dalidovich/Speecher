using Speecher.App;

namespace Speecher;

public class Program
{
    public static int Main(string[] args)
    {
        using var app = new SpeecherApp();
        return app.Run(args);
    }
}
