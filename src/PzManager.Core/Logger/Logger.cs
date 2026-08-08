using System.Diagnostics;

namespace PzManager.Core.Logger;

public static class Log
{
    public static void Info(string message)
    {
        Write(
            ConsoleColor.Gray,
            "INFO",
            message);
    }

    public static void Warn(string message)
    {
        Write(
            ConsoleColor.Yellow,
            "WARN",
            message);

        if (Debugger.IsAttached)
            Debugger.Break();
    }

    public static void Error(string message)
    {
        Write(
            ConsoleColor.Red,
            "ERROR",
            message);

        if (Debugger.IsAttached)
            Debugger.Break();
    }

    private static void Write(
        ConsoleColor color,
        string level,
        string message)
    {
        Console.ForegroundColor = color;

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");

        Console.ResetColor();
    }
}