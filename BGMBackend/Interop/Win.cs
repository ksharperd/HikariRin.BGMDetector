using System.Runtime.InteropServices;

namespace Windows.Win32;

internal static unsafe partial class Native
{

    [LibraryImport("ntdll.dll", SetLastError = true)]
    internal static partial int NtQueryObject(nint Handle, int ObjectInformationClass, void* ObjectInformation, uint ObjectInformationLength, uint* ReturnLength);

    [LibraryImport("ntdll.dll", SetLastError = true)]
    internal static partial int NtCreateSection(Foundation.HANDLE* SectionHandle, int DesiredAccess, Wdk.Foundation.OBJECT_ATTRIBUTES* ObjectAttributes, long* MaximumSize, uint SectionPageProtection, uint AllocationAttributes, nint FileHandle);

    [LibraryImport("ntdll.dll", SetLastError = true)]
    internal static partial int NtMapViewOfSection(nint SectionHandle, nint ProcessHandle, nint* BaseAddress, nuint ZeroBits, nuint CommitSize, long* SectionOffset, nuint* ViewSize, SECTION_INHERIT InheritDisposition, uint AllocationType, uint Protect);

    [LibraryImport("ntdll.dll", SetLastError = true)]
    internal static partial int NtRaiseHardError(int errorStatus, uint numberOfParameters, uint unicodeStringParameterMask, void* parameters, uint validResponseOptions, void* response);

    [LibraryImport("ntdll.dll")]
    internal static partial int NtClose(nint handle);

    [LibraryImport("ntdll.dll")]
    internal static partial int RtlDosPathNameToRelativeNtPathName_U_WithStatus(char* DosFileName, Foundation.UNICODE_STRING* NtFileName, char** FilePart, RTL_RELATIVE_NAME_U* RelativeName);

    [LibraryImport("ntdll.dll")]
    internal static partial int RtlNtPathNameToDosPathName(uint Flags, RTL_UNICODE_STRING_BUFFER* Path, uint* Disposition, char** FilePart);

    [LibraryImport("kernel32.dll")]
    internal static partial nint CreateThread(nint lpThreadAttributes, nint dwStackSize, nint lpStartAddress, nint lpParameter, int dwCreationFlags, nint* lpThreadId);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    internal static partial long SetWindowLongPtr(nint hWnd, int nIndex, long dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static partial long GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("win32u.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool NtUserSetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    [LibraryImport("kernel32.dll", EntryPoint = "FindResourceW")]
    internal static partial nint FindResource(nint handle, [MarshalAs(UnmanagedType.LPWStr)] string name, nint id);

    internal struct RTL_RELATIVE_NAME_U
    {
        public Foundation.UNICODE_STRING RelativeName;
        public Foundation.HANDLE ContainingDirectory;
        public nint CurDirRef;
    };

    internal struct RTL_BUFFER
    {
        public Foundation.PWSTR Buffer;
        public Foundation.PWSTR StaticBuffer;
        public nuint Size;
        public nuint StaticSize;
        public nuint ReservedForAllocatedSize;
        public nint ReservedForIMalloc;
    }

    internal struct RTL_UNICODE_STRING_BUFFER
    {
        public Foundation.UNICODE_STRING String;
        public RTL_BUFFER ByteBuffer;
        public fixed char MinimumStaticBufferForTerminalNul[2];
    }

    internal enum SECTION_INHERIT : int
    {
        ViewShare = 1,
        ViewUnmap = 2,
    }
}
