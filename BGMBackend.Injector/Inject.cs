using Windows.Win32.Foundation;

namespace BGMBackend.Injector;

internal abstract class Inject
{

    public string TargetName = string.Empty;
    public uint TargetPID = 0;

    public abstract void InjectDll(HANDLE hProcess, string dllName);

}
