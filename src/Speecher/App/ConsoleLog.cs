using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Speecher.App;

public static class ConsoleLog
{
    public static void Info(string message) => Console.WriteLine(message);

    public static void Event(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");

    public static void Warning(string message) => Console.WriteLine($"Warning: {message}");

    public static void Error(string message) => Console.WriteLine($"Error: {message}");

    public static string Describe(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: { } status } => $"HTTP {(int)status} {status}",
        HttpRequestException { InnerException: SocketException socket } => $"network error ({socket.SocketErrorCode})",
        HttpRequestException { InnerException: { } inner } => Describe(inner),
        SocketException socket => $"network error ({socket.SocketErrorCode})",
        ExternalException external => $"{external.GetType().Name} 0x{external.ErrorCode:X8}",
        _ => exception.Message
    };
}
