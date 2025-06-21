using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using OpenCCHandle = nint;

namespace BGMBackend.Interop;
internal static unsafe partial class OpenCC
{

    private const string OpenCCLibrary = "OpenCC";

    internal const nint INVALID_HANDLE_VALUE = -1;
#if TARGET_64BIT
    internal static readonly nuint INVALID_SIZE_T_VALUE = unchecked((nuint)unchecked((ulong)-1));
#else
    internal const nuint INVALID_SIZE_T_VALUE = unchecked((uint)-1);
#endif

    [LibraryImport(OpenCCLibrary, EntryPoint = "opencc_open")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial OpenCCHandle Open([MarshalAs(UnmanagedType.LPStr)] string configFileName);

    [LibraryImport(OpenCCLibrary, EntryPoint = "opencc_open_w")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial OpenCCHandle OpenW([MarshalAs(UnmanagedType.LPWStr)] string configFileName);

    [LibraryImport(OpenCCLibrary, EntryPoint = "opencc_close")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Close(OpenCCHandle handle);

    [LibraryImport(OpenCCLibrary, EntryPoint = "opencc_convert_utf8_to_buffer")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nuint ConvertUTF8ToBuffer(OpenCCHandle handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string input, nuint length, ref byte output);

    [LibraryImport(OpenCCLibrary, EntryPoint = "opencc_convert_utf8")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint ConvertUTF8(OpenCCHandle handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string input, nuint length);

    [LibraryImport(OpenCCLibrary, EntryPoint = "opencc_convert_utf8_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void ConvertUTF8Free(nint str);

    [LibraryImport(OpenCCLibrary, EntryPoint = "opencc_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.LPStr)]
    internal static partial string Error();

}
