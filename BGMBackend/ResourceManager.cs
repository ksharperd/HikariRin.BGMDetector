using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;

namespace BGMBackend;
internal static unsafe class ResourceManager
{

    public enum Win32ResourceType : ushort
    {
        Accelerator = 9,
        AnimatedCursor = 21,
        AnimatedIcon = 22,
        Bitmap = 2,
        Cursor = 1,
        Dialog = 5,
        Font = 8,
        FontDir = 7,
        GroupCursor = 12,
        GroupIcon = 14,
        Icon = 3,
        Html = 23,
        Menu = 4,
        Manifest = 24,
        MessageTable = 11,
        UserData = 10,
        String = 6,
        Version = 16,
        PlugAndPlay = 19,
    }

    public static ReadOnlySpan<byte> Load(string name)
    {
        return LoadEx(name, Win32ResourceType.UserData);
    }

    public static ReadOnlySpan<byte> LoadEx(string name, Win32ResourceType type)
    {
        var (pBuffer, bufferSize) = LoadInternal(name, type);
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>((void*)pBuffer), (int)bufferSize);
    }

    private static (nint, uint) LoadInternal(string name, Win32ResourceType type)
    {
        if (Global.SelfBase == nint.Zero)
        {
            return default;
        }

        var hModule = (HMODULE)Global.SelfBase;

        var hResource = (HRSRC)Native.FindResource(hModule, name, (ushort)type);
        if (!hResource.IsNull)
        {
            HGLOBAL hGlobal = Native.LoadResource(hModule, hResource);
            if (!hGlobal.IsNull)
            {
                var size = Native.SizeofResource(hModule, hResource);
                return ((nint)Native.LockResource(hGlobal), size);
            }
        }

        Log.Logger.Fatal($"Failed to load resource: {name}");
        return default;
    }

}
