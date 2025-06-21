using System.Runtime.InteropServices;

namespace BGMBackend.Bridge;

internal static unsafe partial class Methods
{

    public delegate nint* CloudMusicAudioPlayerOnPlayProgress(nint __this, double progress1, double progress2);
    public delegate nint* CloudMusicAudioPlayerSetCurrentLength(nint __this, bool flag, nint _, double length, nint __);

    [UnmanagedCallersOnly]
    internal static nint* CloudMusic_AudioPlayer_OnPlayProgress(nint __this, double progress1, double progress2)
    {
        var result = ((delegate* unmanaged<nint, double, double, nint*>)HookManager.GetOriginRaw<CloudMusicAudioPlayerOnPlayProgress>())(__this, progress1, progress2);

        Log.Logger.Debug($"OnPlayProgress: {progress1}");

        Global.CurrentProgress = (long)(progress1 * 1000);

        return result;
    }

    [UnmanagedCallersOnly]
    internal static nint CloudMusic_AudioPlayer_SetCurrentLength(nint __this, [MarshalAs(UnmanagedType.Bool)] bool flag, nint _, double length, nint __)
    {
        var result = ((delegate* unmanaged<nint, bool, nint, double, nint, nint>)HookManager.GetOriginRaw<CloudMusicAudioPlayerSetCurrentLength>())(__this, flag, _, length, __);

        var v1 = (double*)(__this + 160);
        var v2 = (double*)(__this + 168);

        Log.Logger.Debug($"SetCurrentLength length={length},v1={*v1},v2={*v2}");

        Global.CurrentLength = (long)(*v2 * 1000);

        return result;
    }

}
