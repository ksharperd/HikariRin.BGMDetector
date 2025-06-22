using System.Collections.Generic;
using System.Runtime.CompilerServices;

using BGMBackend.Protocol;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BGMBackend;

internal static unsafe class Global
{

    private static string _currentTitle = string.Empty;

    internal static nint SelfBase;

    internal static HWND ConsoleWindow;

    internal static long CurrentLength = -1;
    internal static long CurrentProgress;

    internal static string CurrentPlayerType = string.Empty;

    internal static bool IsQQMusic;
    internal static bool IsNetEaseMusic;

    internal static BGMProtocol? CurrentBGMProtocol;
    internal static HWND CurrentWindowHandle;
    internal static HWND CurrentMainWindowHandle;

    internal static string CurrentWindowTitle
    {
        get => _currentTitle;
        set
        {
            if (_currentTitle == value)
                return;
            _currentTitle = value;
            CurrentLyric = string.Empty;
            CurrentTsLyric = string.Empty;
            CurrentRomaLyric = string.Empty;
            var currentProtocol = CurrentBGMProtocol;
            currentProtocol?.SetMusicTitle(value);
            if (CurrentMainWindowHandle.IsNull)
            {
                CurrentMainWindowHandle = IsQQMusic ? Native.FindWindow("TXGuiFoundation", value) : HWND.Null;
            }
        }
    }

    internal static bool IsFocused;
    internal static bool IsConsoleOpened;
    internal static string CurrentLyric = string.Empty;
    internal static string CurrentTsLyric = string.Empty;
    internal static string CurrentRomaLyric = string.Empty;
    internal static string OpenCCFakeConfigPath = string.Empty;
    internal static Dictionary<string, string> OpenCCFakeDic = [];
    internal static string OpenCCDefaultConfig = "s2tw";
    internal static bool OpenCCInitialised = false;

    internal static delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> OriginalWndProcHandler;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToggleConsole()
    {
        Native.ShowWindow(ConsoleWindow, IsConsoleOpened ? SHOW_WINDOW_CMD.SW_HIDE : SHOW_WINDOW_CMD.SW_SHOW);
        IsConsoleOpened = !IsConsoleOpened;
    }

}
