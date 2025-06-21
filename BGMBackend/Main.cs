using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using BGMBackend;

using Windows.Win32;

using static BGMBackend.Main;

internal static class Main
{

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = nameof(CreateThread))]
    private static extern bool CreateThread(Thread @this, GCHandle thisThreadHandle);

    private const string MAIN_EXPORT_NAME =
#if NET10_0_OR_GREATER
        "EntryPoint";
#else
        "DllMain";
#endif

    [UnmanagedCallersOnly(EntryPoint = MAIN_EXPORT_NAME, CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe bool EntryPoint(nint hModule, uint ulReasonForCall, nint lpReserved)
    {
        if (ulReasonForCall == 1)
        {
            //var thread = new Thread(() => Init(hModule));
            //GCHandle threadHandle = GCHandle.Alloc(thread);

            //CreateThread(thread, threadHandle);

            delegate* unmanaged<nint, uint> pInit = &Init;
            _ = Native.CreateThread(nint.Zero, 0, (nint)pInit, hModule, 0, null);
        }
        else if (ulReasonForCall == 0)
        {
            HookManager.DetachAll();
        }
        return true;
    }

    //  void ThreadStore::AttachCurrentThread(bool fAcquireThreadStoreLock)
    //  55 8B EC 8B 0D ? ? ? ? 64 A1 ? ? ? ? 56 8B 34 88 81 C6 ? ? ? ? 8B CE E8

}
