using System;
using System.ComponentModel;
using System.Diagnostics;
using Vanara.PInvoke;

namespace SignalPlus.Helpers;

public class ProcessJobTracker : IDisposable
{
    private readonly object _disposeLock = new();
    private bool _disposed;

    /// <summary>
    /// The job handle.
    /// </summary>
    /// <remarks>
    /// Closing this handle would close all tracked processes. So we don't do it in this process
    /// so that it happens automatically when our process exits.
    /// </remarks>
    private readonly Kernel32.SafeHJOB _jobHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessJobTracker"/> class.
    /// </summary>
    public unsafe ProcessJobTracker()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
        {
            return;
        }
        
        // The job name is optional (and can be null) but it helps with diagnostics.
        //  If it's not null, it has to be unique. Use SysInternals' Handle command-line
        //  utility: handle -a ChildProcessTracker
        var jobName = nameof(ProcessJobTracker) + Process.GetCurrentProcess().Id;
        _jobHandle = Kernel32.CreateJobObject(null, jobName);

        var extendedInfo = new Kernel32.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new Kernel32.JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = Kernel32.JOBOBJECT_LIMIT_FLAGS.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        if (!Kernel32.SetInformationJobObject(_jobHandle, Kernel32.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation, (IntPtr)(&extendedInfo), (uint)sizeof(Kernel32.JOBOBJECT_EXTENDED_LIMIT_INFORMATION)))
        {
            throw new Win32Exception();
        }
    }

    /// <summary>
    /// Ensures a given process is killed when the current process exits.
    /// </summary>
    /// <param name="process">The process whose lifetime should never exceed the lifetime of the current process.</param>
    public void AddProcess(Process process)
    {
        if (process is null)
        {
            throw new ArgumentNullException(nameof(process));
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
        {
            var success = Kernel32.AssignProcessToJobObject(_jobHandle, new HPROCESS(process.Handle));
            if (!success && !process.HasExited)
            {
                throw new Win32Exception();
            }
        }
    }

    /// <summary>
    /// Kills all processes previously tracked with <see cref="AddProcess(Process)"/> by closing the Windows Job.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_disposeLock)
            {
                if (!_disposed)
                {
                    _jobHandle?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}