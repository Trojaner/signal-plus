using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SignalPlus.Helpers;

public static class ProcessHelper
{
    public static async Task RunAsync(string workingDirectory, string exe, string args = "", ProcessJobTracker processJobTracker = null, bool waitForExit = false, bool isShellExecute = false)
    {
        workingDirectory = Path.GetFullPath(workingDirectory);

        var processStartInfo = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = isShellExecute,
            RedirectStandardInput = false
        };

        var process = Process.Start(processStartInfo);

        if (waitForExit)
        {
            process!.EnableRaisingEvents = true;
            await process.WaitForExitAsync();
        }

        processJobTracker?.AddProcess(process);
    }
}