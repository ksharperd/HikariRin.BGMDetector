using System.Runtime.InteropServices;

namespace BGMBackend.Interop;

internal static unsafe partial class QQMusic
{

    private const string QQMusicCommonLibrary = "QQMusicCommon";
    internal const string ConfigQuery = "wns_config";

    [DllImport(QQMusicCommonLibrary, EntryPoint = "?des@qqmusic@@YAHPAE0H@Z", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Des(nint pBuffer, nint pKey, int bufferLength);

    [DllImport(QQMusicCommonLibrary, EntryPoint = "?Ddes@qqmusic@@YAHPAE0H@Z", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int DDes(nint pBuffer, nint pKey, int bufferLength);

    [DllImport(QQMusicCommonLibrary, EntryPoint = "?DecryptData@qqmusic@@YAHHPADH@Z", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int DecryptData(int startIndex, nint pBuffer, int bufferLength);

    [DllImport(QQMusicCommonLibrary, EntryPoint = "?GetSharedInstance_ConfigInfo@qqmusic@@YAPAVIConfigInfo@@XZ", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint GetSharedInstance_ConfigInfo();

    [DllImport(QQMusicCommonLibrary, EntryPoint = "?GetIQMPHost@@YAJPAPAUIQMPHost@@@Z", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetIQMPHost(nint* ppQMPHost);

}
