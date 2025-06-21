using System.Runtime.CompilerServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BGMBackend;

internal static unsafe partial class WindowManager
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void HideFromCapture(HWND hWnd)
    {
        Native.NtUserSetWindowDisplayAffinity(hWnd, (uint)WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
    }

}
