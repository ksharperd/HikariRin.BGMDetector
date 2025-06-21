using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BGMBackend;

internal static unsafe partial class HookManager
{

    private const string DetoursLibName = "Detours";

    [LibraryImport(DetoursLibName)]
    internal static partial int DetourTransactionBegin();

    [LibraryImport(DetoursLibName)]
    internal static partial int DetourTransactionAbort();

    [LibraryImport(DetoursLibName)]
    internal static partial int DetourTransactionCommit();


    [LibraryImport(DetoursLibName)]
    internal static partial int DetourUpdateThread(nint hThread);


    [LibraryImport(DetoursLibName)]
    internal static partial int DetourAttach(ref nint ppPointer, nint pDetour);

    [LibraryImport(DetoursLibName)]
    internal static partial int DetourDetach(ref nint ppPointer, nint pDetour);

    private static readonly Dictionary<int, (nint, nint)> MethodDict = [];
    private static readonly Dictionary<int, Delegate> DelegateDict = [];

    public static T GetOrigin<T>() where T : Delegate
    {
        var funcId = typeof(T).GetHashCode();
        ref var @delegate = ref CollectionsMarshal.GetValueRefOrAddDefault(DelegateDict, funcId, out var exists);
        if (exists)
        {
            return (T)@delegate!;
        }
        @delegate = Marshal.GetDelegateForFunctionPointer<T>(MethodDict[funcId].Item1);
        return (T)@delegate!;
    }

    public static nint GetOriginRaw<T>() where T : Delegate
    {
        var funcId = typeof(T).GetHashCode();
        ref (nint, nint) value = ref CollectionsMarshal.GetValueRefOrNullRef(MethodDict, funcId);
        return value.Item1;
    }

    public static void Attach<T>(T origin, T handler) where T : Delegate
    {
        Attach(Marshal.GetFunctionPointerForDelegate(origin), handler);
    }

    public static void Attach<T>(nint origin, T handler) where T : Delegate
    {
        Attach<T>(origin, Marshal.GetFunctionPointerForDelegate(handler));
    }

    public static void Attach<T>(nint origin, nint handler) where T : Delegate
    {
        var funcId = typeof(T).GetHashCode();
        if (MethodDict.ContainsKey(funcId))
        {
            throw new InvalidOperationException($"Duplicate hook for {funcId}.");
        }
        DoAttach(ref origin, handler);
        MethodDict.Add(funcId, (origin, handler));
    }

    public static void Detach<T>() where T : Delegate
    {
        var funcId = typeof(T).GetHashCode();
        var (mPtr, hPtr) = MethodDict[funcId];
        DoDetach(ref mPtr, hPtr);
        MethodDict.Remove(funcId);
    }

    public static void DetachAll()
    {
        var pFunction = nint.Zero;
        foreach (var (_, (originPtr, handlerPtr)) in MethodDict)
        {
            pFunction = originPtr;
            DoDetach(ref pFunction, handlerPtr);
        }

        MethodDict.Clear();
    }

    private static void DoAttach(ref nint mPtr, nint hPtr)
    {
        _ = DetourTransactionBegin();
        _ = DetourAttach(ref mPtr, hPtr);
        _ = DetourTransactionCommit();
    }

    private static void DoDetach(ref nint mPtr, nint hPtr)
    {
        _ = DetourTransactionBegin();
        _ = DetourDetach(ref mPtr, hPtr);
        _ = DetourTransactionCommit();
    }

}
