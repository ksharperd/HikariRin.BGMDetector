using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using BGMBackend.Protocol;

using Reloaded.Memory.Sigscan;

using Windows.Wdk.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.ProcessStatus;
using Windows.Win32.UI.WindowsAndMessaging;

using static BGMBackend.Bridge.Methods;

namespace BGMBackend;


internal static unsafe class Main
{

    private static nint _targetModule;
    private static Scanner? _scanner;

    [UnmanagedCallersOnly]
    internal static uint Init(nint lpThreadParameter)
    {
        Global.SelfBase = lpThreadParameter;
        Native.AllocConsole();
        Native.SetConsoleTitle("開發人員主控台");
        Global.ConsoleWindow = Native.GetConsoleWindow();
#if BGMBACKEND_RELEASE
        Native.ShowWindow(Global.ConsoleWindow, SHOW_WINDOW_CMD.SW_HIDE);
#else
        Global.IsConsoleOpened = true;
#endif

        SetUnhandledExceptionHandler(null, OnUnhandledException);
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        ExceptionHandling.SetUnhandledExceptionHandler(OnUnhandledException);

        var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
        if (moduleName is null)
        {
            return 0;
        }

#if !BGMBACKEND_RELEASE
        if (moduleName.Contains("loader", StringComparison.InvariantCultureIgnoreCase))
        {
            Log.Logger.Debug($"Press any key to preform gc test.");
            Console.ReadKey(true);
            new Thread(DebuggerGcTest).Start();
            new Thread(DebuggerGcTest).Start();
            return 0;
        }
#endif

        var hUser32 = Native.GetModuleHandle("user32.dll");
        delegate* unmanaged[Stdcall]<HWND, PCWSTR, bool> pSetWindowTextW = &SetWindowTextW;
        HookManager.Attach<SetWindowTextWDelegate>(Native.GetProcAddress(hUser32, "SetWindowTextW"), (nint)pSetWindowTextW);

        var hNtDll = Native.GetModuleHandle("ntdll.dll");
#if !BGMBACKEND_RELEASE
        //delegate* unmanaged[Stdcall]<HANDLE, int, NTSTATUS> pNtTerminateThread = &NtTerminateThread;
        //HookManager.Attach<NtTerminateThreadDelegate>(Native.GetProcAddress(hNtDll, "NtTerminateThread"), (nint)pNtTerminateThread);
#endif

        bool result = moduleName switch
        {
            "QQMusic.exe" => InitQQMusic(),
            // TODO: imp for NetEaseMusic
            "cloudmusic.exe" => InitNetEaseMusic(),
            _ => throw new NotImplementedException()
        };
        Log.Logger.Info($"Pre-init done.");

        //delegate* unmanaged[Fastcall]<PCWSTR, int, int, int, nint, byte, HANDLE> pCreateFileInternal = &CreateFileInternal;
        //HookManager.Attach<CreateFileInternalDelegate>(Native.GetProcAddress(hKernelBase, "CreateFileW"), (nint)pCreateFileInternal);
        //Global.OriginalCreateFileInternal = (delegate* unmanaged[Fastcall]<PCWSTR, int, int, int, nint, byte, HANDLE>)HookManager.GetOriginRaw<CreateFileInternalDelegate>();

        PostInit(hNtDll);

        delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> pWndProc = &WndProc;
        nint originalValue =
#if TARGET_64BIT
        (nint)Native.SetWindowLongPtr(Global.CurrentMainWindowHandle, (int)WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (long)pWndProc);
#else
        Native.SetWindowLong(Global.CurrentMainWindowHandle, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (int)pWndProc);
#endif
        if (originalValue != nint.Zero)
        {
            Global.OriginalWndProcHandler = (delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT>)originalValue;
        }
        WindowManager.HideFromCapture(Global.ConsoleWindow);

        return 0;
    }

    private static void PostInit(HMODULE hNtDll)
    {
        delegate* unmanaged[Stdcall]<HANDLE*, int, OBJECT_ATTRIBUTES*, nint, nint*, uint, uint, uint, uint, nint, uint, NTSTATUS> pNtCreateFile = &NtCreateFile;
        HookManager.Attach<NtCreateFileDelegate>(Native.GetProcAddress(hNtDll, "ZwCreateFile"), (nint)pNtCreateFile);

        var client = new JsonRPCClient();
        GC.KeepAlive(client);
    }

    private static bool InitQQMusic()
    {
        Global.CurrentPlayerType = "QQ音乐";
        while ((_targetModule = Native.GetModuleHandle("QQMusic.dll")) == 0)
        {
            continue;
        }
        Log.Logger.Debug($"Target module loaded at 0x{_targetModule:x}");

        nint pQMPHost = 0;
        _ = Interop.QQMusic.GetIQMPHost(&pQMPHost);
        if (pQMPHost == nint.Zero)
        {
            Log.Logger.Error($"Failed to get IQMPHost");
            return false;
        }

        _scanner = CreateScannerFromModule(_targetModule);

        Log.Logger.Debug($"Got IQMPHost at 0x{pQMPHost:x}");
        // since IQMPHost is a COM-like object
        // resolve first virtual call in
        // 55 8B EC 6A ? 68 ? ? ? ? 64 A1 ? ? ? ? 50
        // 83 EC ? 53 56 57 A1 ? ? ? ? 33 C5 50 8D
        // 45 ? 64 A3 ? ? ? ? 8B F9 E8 ? ? ? ? 8B 97
        // to find the right v-slot
        var lpVTableQMPHost = *(nint*)pQMPHost;
        var pfnGetProgressAndDuration = *(nint*)((byte*)lpVTableQMPHost + 88);
        Log.Logger.Debug($"Got IQMPHost->GetProgressAndDuration at 0x{pfnGetProgressAndDuration:x}");
        var GetProgressAndDuration = (delegate* unmanaged[Stdcall]<nint, int*, int*, int>)(pfnGetProgressAndDuration);
        delegate* unmanaged<nint, int*, int*, int> pGetProgressAndDuration = &QQMusic_IQMPHost_GetProgressAndDuration;
        HookManager.Attach<QQMusicIQMPHostGetProgressAndDuration>(pfnGetProgressAndDuration, (nint)pGetProgressAndDuration);

        do
        {
            var hWnd = Native.FindWindow("QQMusic_Daemon_Wnd", null);
            Global.CurrentWindowHandle = hWnd;
            if (!hWnd.IsNull)
            {
                break;
            }
            Thread.Sleep(250);
        }
        while (Global.CurrentWindowHandle == default);

        Global.CurrentBGMProtocol = new QQMusic();
        Global.IsQQMusic = true;

        var buffer = stackalloc char[1024];
        if (Native.GetWindowText(Global.CurrentWindowHandle, buffer, 1024) != 0)
        {
            do
            {
                Global.CurrentWindowTitle = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(buffer).ToString();
                if (!Global.CurrentMainWindowHandle.IsNull)
                {
                    break;
                }
                Thread.Sleep(250);
            }
            while (true);
        }

        return true;
    }

    private static bool InitNetEaseMusic()
    {
        Global.CurrentPlayerType = "网易云音乐";
        while ((_targetModule = Native.GetModuleHandle("cloudmusic.dll")) == 0)
        {
            continue;
        }
        Log.Logger.Debug($"Target module loaded at 0x{_targetModule:x}");

        _scanner = CreateScannerFromModule(_targetModule);

        //uncomment if u wants to debug these buggy code
        //Console.ReadKey(true);

        delegate* unmanaged<nint, bool, nint, double, nint, nint> pNetEaseCloudMusicAudioPlayerSetCurrentLength = &CloudMusic_AudioPlayer_SetCurrentLength;
        delegate* unmanaged<nint, double, double, nint*> pNetEaseCloudMusicAudioPlayerOnPlayProgress = &CloudMusic_AudioPlayer_OnPlayProgress;
        InitFunctionByPattern<CloudMusicAudioPlayerSetCurrentLength>(pNetEaseCloudMusicAudioPlayerSetCurrentLength, "F2 0F 11 5C 24 ?? 44 89 44 24 ?? 88 54 24");
        InitFunctionByPattern<CloudMusicAudioPlayerOnPlayProgress>(pNetEaseCloudMusicAudioPlayerOnPlayProgress, "48 8B C4 53 48 81 EC ?? ?? ?? ?? 0F 29 70 ?? 0F 29 78");

        do
        {
            Global.CurrentWindowHandle = Native.FindWindow("OrpheusBrowserHost", null);
            Thread.Sleep(1000);
        }
        while (Global.CurrentWindowHandle == default);
        Global.CurrentMainWindowHandle = Global.CurrentWindowHandle;

        Global.CurrentBGMProtocol = new CloudMusic();
        Global.IsNetEaseMusic = true;

        return true;
    }

    private static nint InitFunctionByPattern<T>(void* handler, string pattern) where T : Delegate
    {
        var logger = Log.Logger;

        var result = _scanner!.FindPattern(pattern);
        var pFunction = _targetModule + result.Offset;
        if (!result.Found)
        {
            logger.Error($"Failed to find pattern for: {typeof(T).Name}");

#if !BGMBACKEND_RELEASE
            logger.Error($"Pattern: {pattern}");
            logger.Error($"Press any key to continue.");
            Console.ReadKey(true);
#endif
            return nint.Zero;
        }
        if (handler == null)
        {
            return pFunction;
        }
        HookManager.Attach<T>(pFunction, (nint)handler);
        return pFunction;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        OnUnhandledException((Exception)e.ExceptionObject);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Log.ExceptionLogger.Error($"[T({((Task)sender!).Id})] {e.Exception}");
    }

    private static bool OnUnhandledException(Exception exception)
    {
        var nativeThreadId = Native.GetCurrentThreadId();
        var managedThreadId = Environment.CurrentManagedThreadId;
        Log.ExceptionLogger.Fatal($"[N({nativeThreadId})][M({managedThreadId})] {exception}");
        _ = MessageBox($"Exception throws!!!{Environment.NewLine}NativeThreadId: {nativeThreadId}{Environment.NewLine}ManagedThreadId: {managedThreadId}{Environment.NewLine}HResult: {exception.HResult}{Environment.NewLine}{Environment.NewLine}{exception.Message}", "BGMBackend Crashed", 0x00000010);
        Thread.Sleep(8000);
        Environment.Exit(exception.HResult);

        return true;
    }

    internal static int MessageBox(ReadOnlySpan<char> text, ReadOnlySpan<char> caption, uint uType)
    {
        UNICODE_STRING msgBody = new();
        UNICODE_STRING msgCaption = new();
        UNICODE_STRING* pMsgBody = &msgBody;
        UNICODE_STRING* pMsgCaption = &msgCaption;

        fixed (char* pText = text)
        {
            fixed (char* pCaption = caption)
            {
                Native.RtlInitUnicodeString(pMsgBody, pText);
                Native.RtlInitUnicodeString(pMsgCaption, pCaption);
            }
        }

        uint errorResponse = 0U;

        nint* msgParams = stackalloc nint[3];
        msgParams[0] = (nint)pMsgBody;
        msgParams[1] = (nint)pMsgCaption;
        msgParams[2] = (nint)uType;

        _ = Native.NtRaiseHardError(0x50000018, 0x0000003, 3, msgParams, 0, &errorResponse);

        return errorResponse switch
        {
            2 => 3,
            3 => 2,
            4 => 5,
            5 => 7,
            6 => 1,
            7 => 4,
            8 => 6,
            _ => 2,
        };
    }

    private static Scanner CreateScannerFromModule(nint moduleBase)
    {
        Native.GetModuleInformation(Native.GetCurrentProcess(), (HMODULE)moduleBase, out var moduleInfo, (uint)sizeof(MODULEINFO));
        return new Scanner((byte*)moduleBase, (int)moduleInfo.SizeOfImage);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DebuggerGcTest()
    {
        while (true)
        {
            Thread.Sleep(15);
            var str = RandomNumberGenerator.GetHexString(64);
            Debug.WriteLine(str.Substring(2, 60).GetHashCode());
            str.GetHashCode();
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "add_UnhandledException")]
    private static extern void SetUnhandledExceptionHandler([UnsafeAccessorType("System.AppContext, System.Private.CoreLib")] object? c, UnhandledExceptionEventHandler handler);

}
