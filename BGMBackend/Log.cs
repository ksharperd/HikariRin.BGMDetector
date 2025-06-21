using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using Windows.Win32;
using Windows.Win32.System.Console;

namespace BGMBackend;

internal static class Log
{
    private static readonly Lock _lock;
    private static readonly TextWriter? _consoleWriter;
    private static ConsoleLogger? _logger;
    private static FileLogger? _fileLogger;

    static unsafe Log()
    {
        _lock = new();

        CONSOLE_MODE mode;
        var handle = Native.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
        if (!Native.GetConsoleMode(handle, &mode)) return;
        Native.SetConsoleMode(handle, mode | CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        _consoleWriter = Console.Out;
    }

    internal static ConsoleLogger Logger
    {
        get
        {
#if BGMBACKEND_RELEASE
            _logger ??= new(LogLevel.Information);
#else
            _logger ??= new(LogLevel.Debug);
#endif
            return _logger;
        }
    }

    internal static FileLogger ExceptionLogger
    {
        get
        {
            _fileLogger ??= new(LogLevel.Warning, Path.Combine(AppContext.BaseDirectory, "BGMBackend.log"));

            return _fileLogger;
        }
    }

    internal enum LogLevel : int
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3,
        Fatal = 4,
        Silent = 5
    }

    internal sealed class ConsoleLogger(LogLevel Level)
    {

        private static readonly string _loggerClass = nameof(BGMBackend);

        public void Warn(ReadOnlySpan<char> message)
        {
            if (Level > LogLevel.Warning)
            {
                return;
            }
            WriteLogCore(message, _loggerClass);
        }

        public void Error(ReadOnlySpan<char> message)
        {
            if (Level > LogLevel.Error)
            {
                return;
            }
            WriteLogCore(message, _loggerClass);
        }

        public void Fatal(ReadOnlySpan<char> message)
        {
            if (Level > LogLevel.Fatal)
            {
                return;
            }
            WriteLogCore(message, _loggerClass);
        }

        public void Info(ReadOnlySpan<char> message)
        {
            if (Level > LogLevel.Information)
            {
                return;
            }
            WriteLogCore(message, _loggerClass);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#pragma warning disable CA1822
        public void Debug(ReadOnlySpan<char> message)
#pragma warning restore CA1822
        {
#if !BGMBACKEND_RELEASE
            if (Level > LogLevel.Debug)
            {
                return;
            }
            WriteLogCore(message, _loggerClass);
#endif
        }
    }

    internal sealed class FileLogger
    {

        private static readonly string _loggerClass = "WatchDog";

        public LogLevel Level { get; set; }

        public string LogFile { get; set; }

        private readonly FileStream? _stream;

        public FileLogger(LogLevel level, string logFile)
        {
            if (!Path.Exists(logFile))
            {
                _stream = new(logFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            }

            Level = level;
            LogFile = logFile;
        }

        public void Warn(ReadOnlySpan<char> message)
        {
            if (Level > LogLevel.Warning)
            {
                return;
            }
            WriteLogCore(message, _loggerClass, false, true, _stream);
        }

        public void Error(ReadOnlySpan<char> message)
        {
            if (Level > LogLevel.Error)
            {
                return;
            }
            WriteLogCore(message, _loggerClass, false, true, _stream);
        }

        public void Fatal(ReadOnlySpan<char> message)
        {
            if (Level > LogLevel.Fatal)
            {
                return;
            }
            WriteLogCore(message, _loggerClass, false, true, _stream);
        }
    }

    private static unsafe void WriteLogCore(ReadOnlySpan<char> message, string tag, bool writeToConsole = true, bool writeToLogFile = false, FileStream? fileStream = null, [CallerMemberName] string level = "")
    {
        var time = DateTimeOffset.Now.ToString("HH:mm:ss.fff");

        lock (_lock)
        {
            if (writeToConsole)
            {
                var color = level switch
                {
                    "Fatal" => "1;91",
                    "Error" => "1;31",
                    "Warn" => "93",
                    "Info" => "94",
                    "Debug" => "38;5;43",
                    "Trace" => "95",
                    _ => throw new ArgumentException($"Invalid log level: {level}")
                };
                var levelStr = $"\u001b[{color}m{level,5}\u001b[0m";
                _consoleWriter!.WriteLine($"[{time}][{levelStr}] {tag} : {message}");
            }

            if (writeToLogFile)
            {
                ArgumentNullException.ThrowIfNull(fileStream);
                fileStream.Write(Encoding.UTF8.GetBytes($"[{time}][{level}] {tag} : {message}"));
                fileStream.Flush();
            }
        }
    }

}
