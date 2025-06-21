using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using WmiLight;

namespace BGMBackend.Injector
{
    internal class ProcessWatcher : IDisposable
    {
        static readonly Lock _lock = new();

        [field: AllowNull]
        public static ProcessWatcher Instance
        {
            get
            {
                lock (_lock)
                {
                    field ??= CreateInstance();
                }
                return field;
            }
        }

        private readonly WmiConnection? wmiConnection;
        private readonly WmiEventWatcher? processStartWatcher;
        private readonly WmiEventWatcher? processDestroyWatcher;

        public struct ProcessEventInfo
        {
            public string Executable;
            public string Name;
            public uint ProcessId;
            public bool IsStart;
        }
        public event EventHandler<ProcessEventInfo>? ProcessStart;
        public event EventHandler<ProcessEventInfo>? ProcessExit;

        private ProcessWatcher()
        {
            wmiConnection = new();
            processStartWatcher = wmiConnection.CreateEventWatcher("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_Process'");
            processDestroyWatcher = wmiConnection.CreateEventWatcher("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_Process'");

            processStartWatcher.EventArrived += OnEventArrived;
            processDestroyWatcher.EventArrived += OnEventArrived;
        }

        private void OnEventArrived(object? sender, WmiEventArrivedEventArgs e)
        {
            var targetInstance = e.NewEvent["TargetInstance"] as WmiObject;
            var exePath = (string)targetInstance!["ExecutablePath"];
            var processName = (string)targetInstance["Name"];
            var processId = (uint)targetInstance["ProcessId"];
            var eventInfo = new ProcessEventInfo()
            {
                Executable = exePath,
                Name = processName,
                ProcessId = processId,
                IsStart = sender == processStartWatcher
            };

            if (eventInfo.IsStart)
            {
                ProcessStart?.Invoke(sender, eventInfo);
            }
            else
            {
                ProcessExit?.Invoke(sender, eventInfo);
            }
        }

        public void Start()
        {
            processStartWatcher?.Start();
            processDestroyWatcher?.Start();
        }

        public void Stop()
        {
            processStartWatcher?.Stop();
            processDestroyWatcher?.Stop();
        }

        public void Dispose()
        {
            Stop();
            processStartWatcher?.Dispose();
            processDestroyWatcher?.Dispose();
            wmiConnection?.Dispose();
        }

        [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
        private static extern ProcessWatcher CreateInstance();
    }
}
