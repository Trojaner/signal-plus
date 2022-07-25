using System.Linq;
using System.ServiceProcess;

namespace SignalPlus.Helpers;

public static class ServiceHelper
{
    public static ServiceController GetService(string name)
    {
        var services = ServiceController.GetServices().Concat(ServiceController.GetDevices());
        return services.FirstOrDefault(s => s.ServiceName == name);
    }

    public static bool IsServiceInstalled(string name)
    {
        return GetService(name) != null;
    }

    public static bool IsServiceRunning(string name)
    {
        return GetService(name) is { Status: ServiceControllerStatus.Running };
    }
}