using System.Runtime.InteropServices;

namespace BGMBackend.Bridge;
internal static unsafe partial class Methods
{

    public delegate int QQMusicIQMPHostGetProgressAndDuration(nint @this, int* pTotalListenTime, int* pDuration);

    [UnmanagedCallersOnly]
    internal static int QQMusic_IQMPHost_GetProgressAndDuration(nint @this, int* pTotalListenTime, int* pDuration)
    {
        var result = ((delegate* unmanaged<nint, int*, int*, int>)HookManager.GetOriginRaw<QQMusicIQMPHostGetProgressAndDuration>())(@this, pTotalListenTime, pDuration);

        Global.CurrentLength = *pDuration;
        Global.CurrentProgress = *pTotalListenTime;

        return result;
    }

}
