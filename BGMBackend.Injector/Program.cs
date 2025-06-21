using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using AsmResolver.PE;

using BGMBackend.Injector.Injections;

using Microsoft.Win32;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.ProcessStatus;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BGMBackend.Injector;

internal unsafe partial class Program
{
    const string INJECT_DLL_NAME_X64 = "BGMBackend.dll";
    const string INJECT_DLL_NAME_X86 = "BGMBackend32.dll";

    static readonly SearchValues<string> SupportedProcessNames = SearchValues.Create(["QQMusic.exe", "cloudmusic.exe"], StringComparison.OrdinalIgnoreCase);
    static string _lastInjectProcessName = string.Empty;
    static uint _lastInjectProcessId = 0;

    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        ExceptionHandling.SetUnhandledExceptionHandler(OnUnhandledException);
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var task = Task.Factory.StartNew(AppMain);

        var processes = Process.GetProcesses().Where(SupportedProcessesFilter);
        var countOfTarget = processes.Count();
        if (countOfTarget > 1)
        {
            Log.Error("More than one target found!");
            return;
        }
        if (countOfTarget != 0)
        {
            var targetProcess = processes.First();
            var basePath = new FileInfo(targetProcess.MainModule!.FileName).DirectoryName!;
            TryInvokeProcessStartHandler(targetProcess.MainModule.ModuleName, basePath, (uint)targetProcess.Id);
        }

        var processWatcher = ProcessWatcher.Instance;
        processWatcher.ProcessStart += OnProcessEvent;
        processWatcher.ProcessExit += OnProcessEvent;
        ProcessWatcher.Instance.Start();

        while (!task.IsCompleted) { Thread.Sleep(1); }

        processWatcher.Dispose();
    }

    private static void OnProcessEvent(object? sender, ProcessWatcher.ProcessEventInfo e)
    {
        if (string.IsNullOrEmpty(e.Executable))
        {
            return;
        }
        if (!SupportedProcessNames.Contains(e.Name))
        {
            return;
        }
        var basePath = new FileInfo(e.Executable).DirectoryName!;
        if (e.IsStart)
        {
            TryInvokeProcessStartHandler(e.Name, basePath, e.ProcessId);
        }
        else
        {
            TryInvokeProcessDestroyHandler(e.Name, basePath, e.ProcessId);
        }
    }

    private static void TryInvokeProcessStartHandler(string name, string path, uint id)
    {
        try
        {
            var targetProcess = Process.GetProcessById((int)id);
            if (targetProcess.Modules.OfType<ProcessModule>().Any(module => module.ModuleName.Contains("BGMBackend", StringComparison.OrdinalIgnoreCase)))
            {
                _lastInjectProcessName = name;
                _lastInjectProcessId = id;
                return;
            }
            if (HandleProcessStart(name, path, id))
            {
                _lastInjectProcessName = name;
                _lastInjectProcessId = id;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}");
        }
    }

    private static void TryInvokeProcessDestroyHandler(string name, string path, uint id)
    {
        try
        {
            HandleProcessDestroy(name, path, id);
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}");
        }
    }

    static bool HandleProcessStart(string name, string path, uint id)
    {
        if (_lastInjectProcessId != 0)
        {
            var lastProcess = Process.GetProcessById((int)_lastInjectProcessId);
            if (lastProcess.MainModule is { } mainModule)
            {
                if (mainModule.ModuleName == _lastInjectProcessName)
                {
                    return false;
                }
            }
        }

        var hTargetProcess = Native.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_ALL_ACCESS, false, id);
        Native.IsWow64Process2(hTargetProcess, out var targetProcessMachine, null);
        bool targetIs32bitProcess = targetProcessMachine == IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_I386;
        ENUM_PROCESS_MODULES_EX_FLAGS moduleEnumFlag = ENUM_PROCESS_MODULES_EX_FLAGS.LIST_MODULES_ALL;
        string dllToInject = targetIs32bitProcess ? INJECT_DLL_NAME_X86 : INJECT_DLL_NAME_X64;
        bool compatMode = Environment.Is64BitProcess && targetIs32bitProcess;
        if (compatMode)
        {
            moduleEnumFlag = ENUM_PROCESS_MODULES_EX_FLAGS.LIST_MODULES_32BIT;
        }
        else if (!Environment.Is64BitProcess && !targetIs32bitProcess)
        {
            throw new NotSupportedException("Target a 64bit process within 32bit process is not supported.");
        }
        dllToInject = Path.Combine(AppContext.BaseDirectory, dllToInject);
        if (!File.Exists(dllToInject))
        {
            throw new FileNotFoundException($"Could not find critical module {dllToInject}.");
        }

        nint pLoadLibraryW = nint.Zero;
        if (compatMode)
        {
            //Log.Debug($"Using 32bit compat mode for {name}.");
            uint bufferLength = 0;
            Native.EnumProcessModulesEx(hTargetProcess, null, 0, &bufferLength, moduleEnumFlag);
            var moduleCount = (int)bufferLength / sizeof(HMODULE);
            var modulesBuffer = stackalloc HMODULE[moduleCount];
            Native.EnumProcessModulesEx(hTargetProcess, modulesBuffer, bufferLength, &bufferLength, moduleEnumFlag);
            var modules = MemoryMarshal.CreateSpan(ref Unsafe.AsRef<HMODULE>(modulesBuffer), moduleCount);
            var moduleNameBuffer = stackalloc char[260];
            bool success = false;
            foreach (var module in modules)
            {
                if (module.IsNull)
                {
                    continue;
                }

                NativeMemory.Clear(moduleNameBuffer, 260 * sizeof(char));
                if (Native.GetModuleFileNameEx(hTargetProcess, module, moduleNameBuffer, 260) == 0)
                {
                    continue;
                }

                var moduleName = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(moduleNameBuffer).ToString();
                if (moduleName.EndsWith("KernelBase.dll", StringComparison.OrdinalIgnoreCase))
                {
                    //Log.Debug("Found target module.");
                    moduleName = moduleName.Replace("System32", "SysWOW64");
                    using var fileReadStream = File.Open(moduleName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using BinaryReader reader = new(fileReadStream);
                    var peImage = PEImage.FromBytes(reader.ReadBytes((int)fileReadStream.Length));
                    var targetExport = peImage.Exports!.Entries.Where(export => export.IsByName && (export.Name == "LoadLibraryW")).First();
                    pLoadLibraryW = (nint)((nint)module.Value + targetExport.Address.Rva);

                    success = true;
                }
            }
            if (!success)
            {
                throw new FileNotFoundException("Could not find kernel module.");
            }
        }
        else
        {
            var hKernelBase = Native.GetModuleHandle("KernelBase.dll");
            pLoadLibraryW = Native.GetProcAddress(hKernelBase, "LoadLibraryW");
        }

        var inject = new LoadLibrary(pLoadLibraryW)
        {
            TargetName = name,
            TargetPID = id
        };
        inject.InjectDll(hTargetProcess, dllToInject);

        Native.CloseHandle(hTargetProcess);
        return true;
    }

    static void HandleProcessDestroy(string name, string path, uint id)
    {
        if ((id == _lastInjectProcessId) || (name == _lastInjectProcessName))
        {
            _lastInjectProcessId = 0;
            _lastInjectProcessName = string.Empty;
        }
    }

    private static bool SupportedProcessesFilter(Process process)
    {
        if (process.Id < 500)
        {
            return false;
        }
        try
        {
            if (process.MainModule is null)
            {
                return false;
            }
            if (SupportedProcessNames.Contains(process.MainModule.ModuleName))
            {
                return true;
            }
        }
        catch (Exception)
        {
        }

        return false;
    }

    private static void AppMain()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        string key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        string valueName = "AppsUseLightTheme";
        bool isDarkTheme = false;
        using (RegistryKey? themeKey = Registry.CurrentUser.OpenSubKey(key))
        {
            var value = themeKey?.GetValue(valueName);
            if (value is not null)
            {
                isDarkTheme = Convert.ToInt32(value) == 0;
            }
        }

        var notifyIcon = Log.NotifyIcon;
        notifyIcon.Visible = true;
        notifyIcon.Text = "BGM 延伸模組";
        var contextMenuStrip = new ContextMenuStrip
        {
            Renderer = isDarkTheme ? new DarkBlueRenderer() : new LightBlueRenderer()
        };
        contextMenuStrip.Items.Add("關閉 BGM 延伸模組", null, (object? sender, EventArgs e) => { Application.Exit(); });
        notifyIcon.ContextMenuStrip = contextMenuStrip;

        var selfExecutableFileInfo = new FileInfo(Environment.GetCommandLineArgs()[0]);
        var selfExecutablePath = Environment.GetCommandLineArgs()[0];
        selfExecutablePath = selfExecutablePath[..^3] + "exe";
        HICON hLargeIcon, hSmallIcon;
        Native.ExtractIconEx(selfExecutablePath, 0, &hLargeIcon, &hSmallIcon, 1);

        notifyIcon.Icon = Icon.FromHandle(hLargeIcon);

        Application.Run();

        notifyIcon.Visible = false;
        Native.DestroyIcon(hLargeIcon);
        Native.DestroyIcon(hSmallIcon);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        OnUnhandledException(e.Exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        OnUnhandledException((Exception)e.ExceptionObject);
    }

    private static bool OnUnhandledException(Exception ex)
    {
        if (ex is Win32Exception)
        {
            return true;
        }
        Log.Error(ex);
        return true;
    }

}
