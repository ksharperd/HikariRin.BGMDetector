using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.System.Console;
using Windows.Win32.System.Diagnostics.Debug;

namespace BGMBackend.Injector;

internal static class Log
{

    private static readonly unsafe delegate* unmanaged[Stdcall]<uint> GetLastError;
    public static NotifyIcon NotifyIcon = new();

    const string DEFAULT_LOG_TAG = "WatchDog";
    const int DEFAULT_LANG_ID = 1024;

    static unsafe Log()
    {
        CONSOLE_MODE mode;
        var handle = Native.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
        if (!Native.GetConsoleMode(handle, &mode)) return;
        Native.SetConsoleMode(handle, mode | CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        GetLastError = (delegate* unmanaged[Stdcall]<uint>)NativeLibrary.GetExport(NativeLibrary.Load("kernel32.dll"), "GetLastError");
    }

    public static void Error(object value, Exception? exception = null, string callerName = DEFAULT_LOG_TAG)
    {
        NotifyIcon.BalloonTipTitle = callerName;
        WriteLog(value, exception, callerName);
    }

    public static void Warn(object value, Exception? exception = null, string callerName = DEFAULT_LOG_TAG)
    {
        NotifyIcon.BalloonTipTitle = callerName;
        WriteLog(value, exception, callerName);
    }

    public static void Info(object value, Exception? exception = null, string callerName = DEFAULT_LOG_TAG)
    {
        NotifyIcon.BalloonTipTitle = callerName;
        WriteLog(value, exception, callerName);
    }

    public static void Debug(object value, Exception? exception = null, string callerName = DEFAULT_LOG_TAG)
    {
        NotifyIcon.BalloonTipTitle = callerName;
        WriteLog(value, exception, callerName);
    }

    public static void Trace(object value, Exception? exception = null, [CallerMemberName] string callerName = DEFAULT_LOG_TAG)
    {
        NotifyIcon.BalloonTipTitle = callerName;
        WriteLog(value, exception, callerName);
    }

    public static unsafe ReadOnlySpan<char> GetLastErrorAsString()
    {
        char* messageBuffer = null;
        var error = GetLastError();
        var size = Native.FormatMessage(FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_IGNORE_INSERTS, null, error, DEFAULT_LANG_ID, (char*)&messageBuffer, 0, null);
        return new(messageBuffer, (int)size);
    }

    private static void WriteLog(object message, Exception? exception, string tag, [CallerMemberName] string level = "")
    {
        var icon = level switch
        {
            "Error" => ToolTipIcon.Error,
            "Warn" => ToolTipIcon.Warning,
            "Info" => ToolTipIcon.Info,
            "Debug" => ToolTipIcon.Info,
            "Trace" => ToolTipIcon.None,
            _ => throw new ArgumentException($"Invalid log level: {level}")
        };
        var colour = level switch
        {
            "Error" => "1;31",
            "Warn" => "93",
            "Info" => "94",
            "Debug" => "38;5;43",
            "Trace" => "95",
            _ => throw new ArgumentException($"Invalid log level: {level}")
        };
        var levelStr = $"\u001b[{colour}m{level,5}\u001b[0m";
        var time = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{time}][{levelStr}] {tag} : {message}");
        if (exception != null)
        {
            Console.WriteLine(exception.ToString());
        }
        NotifyIcon.BalloonTipIcon = icon;
        NotifyIcon.BalloonTipText = message.ToString()!;
        NotifyIcon.ShowBalloonTip(5000);
    }

}
