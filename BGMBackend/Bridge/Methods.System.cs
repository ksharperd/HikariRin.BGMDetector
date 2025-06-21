using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Windows.Wdk.Foundation;
using Windows.Win32.Foundation;

using static Windows.Win32.Native;

namespace BGMBackend.Bridge;

internal static unsafe partial class Methods
{

    public delegate bool SetWindowTextWDelegate(HWND hWND, PCWSTR lpString);
    public delegate NTSTATUS NtCreateFileDelegate(HANDLE* FileHandle, int DesiredAccess, OBJECT_ATTRIBUTES* ObjectAttributes, nint IoStatusBlock, nint* AllocationSize, uint FileAttributes, uint ShareAccess, uint CreateDisposition, uint CreateOptions, nint EaBuffer, uint EaLength);
    public delegate NTSTATUS NtTerminateThreadDelegate(HANDLE hThread, int dwExitCode);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static bool SetWindowTextW(HWND hWND, PCWSTR lpString)
    {
        var result = ((delegate* unmanaged<HWND, PCWSTR, bool>)HookManager.GetOriginRaw<SetWindowTextWDelegate>())(hWND, lpString);

        if (!Global.CurrentWindowHandle.IsNull && (hWND == Global.CurrentWindowHandle))
        {
            var title = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(lpString.Value).ToString();
            Global.CurrentWindowTitle = title;
            Log.Logger.Debug($"Try to set window title to {title}");
        }

        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static LRESULT WndProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {

        switch (uMsg)
        {
            case 0x0006:    // WM_ACTIVATE
                Global.IsFocused = wParam.Value != nuint.Zero;
                break;
            case 0x0101:    // WM_KEYUP
                if ((wParam == 0xC0) || (wParam == 0x7E)) // VK_OEM_3 || VK_F15
                {
                    Global.ToggleConsole();
                }
                break;
            default:
                break;
        }

        if (Global.OriginalWndProcHandler is not null)
        {
            return Global.OriginalWndProcHandler(hWnd, uMsg, wParam, lParam);
        }

        return (LRESULT)nint.Zero;

    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static NTSTATUS NtCreateFile(HANDLE* FileHandle, int DesiredAccess, OBJECT_ATTRIBUTES* ObjectAttributes, nint IoStatusBlock, nint* AllocationSize, uint FileAttributes, uint ShareAccess, uint CreateDisposition, uint CreateOptions, nint EaBuffer, uint EaLength)
    {
        var pOrigin = (delegate* unmanaged[Stdcall]<HANDLE*, int, OBJECT_ATTRIBUTES*, nint, nint*, uint, uint, uint, uint, nint, uint, NTSTATUS>)HookManager.GetOriginRaw<NtCreateFileDelegate>();

        var lpFileName = ObjectAttributes->ObjectName->Buffer;
        var managed = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(lpFileName);
        var fileInfo = new FileInfo(managed.ToString());
        var fileName = fileInfo.Name;
        var isConfig = fileName.Contains(Global.OpenCCDefaultConfig);
        var isDic = fileName.Contains(".ocd2");
        var isQQMusicLyric = fileName.Contains(".qrc");
        if (managed.IsEmpty || (!isConfig && !isDic))
        {
            if (isQQMusicLyric)
            {
                var fullName = fileInfo.FullName;
                if (fileName.EndsWith("qm.qrc") && (fullName != Global.CurrentLyric))
                {
                    Log.Logger.Debug($"Load lyric: {fullName}");
                    Global.CurrentLyric = fullName;
                }
                else if (fileName.EndsWith("ts.qrc") && (fullName != Global.CurrentTsLyric))
                {
                    Log.Logger.Debug($"Load ts lyric: {fullName}");
                    Global.CurrentTsLyric = fullName;
                }
                else if (fileName.EndsWith("Roma.qrc") && (fullName != Global.CurrentRomaLyric))
                {
                    Log.Logger.Debug($"Load roma lyric: {fullName}");
                    Global.CurrentRomaLyric = fullName;
                }
            }
            var @return = pOrigin(FileHandle, DesiredAccess, ObjectAttributes, IoStatusBlock, AllocationSize, FileAttributes, ShareAccess, CreateDisposition, CreateOptions, EaBuffer, EaLength);
            return @return;
        }

#if !BGMBACKEND_RELEASE
        Log.Logger.Debug($"[OpenCC] File io: {fileName}");
#endif
        string fakeFile = string.Empty;
        if (isDic)
        {
            ref var realFilePath = ref CollectionsMarshal.GetValueRefOrAddDefault(Global.OpenCCFakeDic, fileName, out var exists);
            if (exists)
            {
                fakeFile = realFilePath!;
            }
            else
            {
                realFilePath = Path.GetTempFileName();
                fakeFile = realFilePath;
                Log.Logger.Info($"[OpenCC] Setup dic file: {fileName}");
                var resBuffer = ResourceManager.Load("OPENCC_DIC_" + fileName.Replace(".ocd2", string.Empty).ToUpperInvariant());
                File.WriteAllBytes(fakeFile, resBuffer);
            }
        }
        else if (isConfig)
        {
            fakeFile = Global.OpenCCFakeConfigPath;
            if (fakeFile.Length == 0)
            {
                fakeFile = Path.GetTempFileName();
                Log.Logger.Info($"[OpenCC] Setup config file: {fileName}");
                var resBuffer = ResourceManager.Load("OPENCC_CONFIG_" + Global.OpenCCDefaultConfig.ToUpperInvariant());
                File.WriteAllBytes(fakeFile, resBuffer);
                Global.OpenCCFakeConfigPath = fakeFile;
            }
        }
        var fakeFileNtPath = DosPathToNtPath(fakeFile, out var relativeName);
#if !BGMBACKEND_RELEASE
        fixed (char* lpNtFileName = fakeFileNtPath)
        {
            var dbgString = new PCWSTR(lpNtFileName);
        }
        Log.Logger.Debug($"[OpenCC] Redirect to: {fakeFileNtPath}");
#endif
        UNICODE_STRING fakeFileNtPathU = new();

        RtlInitUnicodeString(ref fakeFileNtPathU, fakeFileNtPath);
        if (relativeName.RelativeName.Length != 0)
        {
            ObjectAttributes->RootDirectory = relativeName.ContainingDirectory;
            ObjectAttributes->ObjectName = &relativeName.RelativeName;
        }
        else
        {
            ObjectAttributes->RootDirectory = HANDLE.Null;
            ObjectAttributes->ObjectName = &fakeFileNtPathU;
        }
        var result = pOrigin(FileHandle, DesiredAccess, ObjectAttributes, IoStatusBlock, AllocationSize, FileAttributes, ShareAccess, CreateDisposition, CreateOptions, EaBuffer, EaLength);
#if !BGMBACKEND_RELEASE
        Log.Logger.Debug($"[OpenCC] Redirect result: {result}");
#endif
        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static NTSTATUS NtTerminateThread(HANDLE hThread, int dwExitCode)
    {

        Log.Logger.Warn($"Someone try to terminate thread {(nint)hThread.Value} with exit code {dwExitCode}.");

        return (NTSTATUS)0;

    }

    private static string NtPathToDosPath(UNICODE_STRING ntPath)
    {
        RTL_UNICODE_STRING_BUFFER dosPath = new();
        var bufferSize = ntPath.Length + sizeof(char);
        void* dosPathBuffer = stackalloc byte[bufferSize];
        void* ntPathBuffer = stackalloc byte[bufferSize];

        NativeMemory.Copy(ntPath.Buffer, dosPathBuffer, ntPath.Length);
        NativeMemory.Copy(ntPath.Buffer, ntPathBuffer, ntPath.Length);

        dosPath.ByteBuffer.Buffer = (char*)dosPathBuffer;
        dosPath.ByteBuffer.StaticBuffer = (char*)ntPathBuffer;
        dosPath.String.Buffer = ntPath.Buffer;
        dosPath.String.Length = ntPath.Length;
        dosPath.String.MaximumLength = ntPath.Length;
        dosPath.ByteBuffer.Size = ntPath.Length;
        dosPath.ByteBuffer.StaticSize = ntPath.Length;

        RtlNtPathNameToDosPathName(0, &dosPath, null, null);

        return MemoryMarshal.CreateReadOnlySpanFromNullTerminated((char*)dosPathBuffer).ToString();
    }

    private static string DosPathToNtPath(string dosPath, out RTL_RELATIVE_NAME_U relativeName)
    {
        UNICODE_STRING outBuffer = new();
        relativeName = new();
        fixed (char* pDosPath = dosPath)
        fixed (RTL_RELATIVE_NAME_U* pRelativeName = &relativeName)
        {
            var filePartBuffer = stackalloc char[260];
            RtlDosPathNameToRelativeNtPathName_U_WithStatus(pDosPath, &outBuffer, null, pRelativeName);

            return outBuffer.Buffer.ToString();
        }
    }

}
