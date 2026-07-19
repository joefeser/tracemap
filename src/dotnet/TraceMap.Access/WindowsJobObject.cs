using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TraceMap.Access;

internal sealed class WindowsJobObject
{
    private static readonly object ActiveJobsLock = new();
    private static readonly List<WindowsJobObject> ActiveJobs = [];
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint ProcessSetQuota = 0x0100;
    private const uint ProcessTerminate = 0x0001;

    private readonly SafeFileHandle _handle;

    private WindowsJobObject(SafeFileHandle handle) => _handle = handle;

    public static WindowsJobObject? TryCreateForCurrentProcess()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid) return null;
        var job = new WindowsJobObject(handle);
        try
        {
            var information = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = JobObjectLimitKillOnJobClose }
            };
            var length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
            var pointer = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(information, pointer, false);
                if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformationClass, pointer, (uint)length))
                {
                    handle.Dispose();
                    return null;
                }
            }
            finally { Marshal.FreeHGlobal(pointer); }

            using var current = new SafeFileHandle(GetCurrentProcess(), ownsHandle: false);
            if (!AssignProcessToJobObject(handle, current))
            {
                handle.Dispose();
                return null;
            }
            // A kill-on-close job must remain rooted until the worker exits. The worker is
            // short-lived, so retaining one handle per invocation is intentional.
            lock (ActiveJobsLock) ActiveJobs.Add(job);
            return job;
        }
        catch
        {
            handle.Dispose();
            return null;
        }
    }

    public bool TryAssign(int processId)
    {
        var process = OpenProcess(ProcessSetQuota | ProcessTerminate, false, processId);
        if (process.IsInvalid) return false;
        using (process) return AssignProcessToJobObject(_handle, process);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeFileHandle process);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle OpenProcess(uint access, bool inheritHandle, int processId);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
