using System.ComponentModel;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace BGMBackend.Injector.Injections;

internal sealed class LoadLibrary(nint pRemoteLoadLibraryW) : Inject
{
    public override unsafe void InjectDll(HANDLE hProcess, string dllName)
    {
        if (pRemoteLoadLibraryW == nint.Zero)
        {
            throw new InvalidOperationException("Could not perform this operation before setting a valid function address.");
        }

        var pathLength = (nuint)dllName.Length * sizeof(char);
        var pDllPath = Native.VirtualAllocEx(hProcess, null, pathLength, VIRTUAL_ALLOCATION_TYPE.MEM_RESERVE | VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT, PAGE_PROTECTION_FLAGS.PAGE_READWRITE);
        if (pDllPath == null)
        {
            throw new InvalidOperationException("Failed to allocate memory for DLLPath in target process.");
        }

        fixed (char* pStr = dllName)
        {
            if (!Native.WriteProcessMemory(hProcess, pDllPath, pStr, pathLength, null))
            {
                throw new InvalidOperationException("Failed to write remote process memory.");
            }
        }

        var hThread = CreateRemoteThread(hProcess, 0, 0, pRemoteLoadLibraryW, pDllPath, 0, null);
        if (hThread.IsNull)
        {
            Native.VirtualFreeEx(hProcess, pDllPath, 0, VIRTUAL_FREE_TYPE.MEM_RELEASE);
            throw new Win32Exception("Failed to create remote thread.");
        }

        if (Native.WaitForSingleObject(hThread, 3000) == WAIT_EVENT.WAIT_OBJECT_0)
        {
            //Log.Info("Remote thread ended successfully.");
            Native.VirtualFreeEx(hProcess, pDllPath, 0, VIRTUAL_FREE_TYPE.MEM_RELEASE);
        }

        Native.CloseHandle(hThread);
        Log.Info($"Successfully setup for {TargetName}({TargetPID})");
    }

    [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern unsafe HANDLE CreateRemoteThread(nint hProcess, nint lpThreadAttributes, nuint dwStackSize, nint lpStartAddress, void* lpParameter, uint dwCreationFlags, uint* lpThreadId);
}
