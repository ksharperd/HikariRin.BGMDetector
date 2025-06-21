#include "pch.h"

EXTERN_C_START

struct Wrapper
{
    HMODULE Module;
    DWORD Reason;
    LPVOID Reserved;
};

BOOL APIENTRY EntryPoint(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved);

void ManagedEntryPointWrapper(LPVOID Arg)
{
    auto arg = (Wrapper *)Arg;
    Sleep(3000);
    EntryPoint(arg->Module, arg->Reason, arg->Reserved);
}

EXTERN_C_END

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    if (ul_reason_for_call < 2)
    {
        if (ul_reason_for_call == DLL_PROCESS_ATTACH)
        {
            CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)ManagedEntryPointWrapper, new Wrapper{hModule, ul_reason_for_call, lpReserved}, 0, NULL);
        }
        else
        {
            EntryPoint(hModule, ul_reason_for_call, lpReserved);
        }
    }
    return TRUE;
}
