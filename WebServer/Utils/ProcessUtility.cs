using System.Diagnostics;
using System.Management;

namespace WebServer.Utils
{
    public static class ProcessUtility
    { 
        /// <summary>
        /// Get Parent Process on Windows OS machine
        /// </summary>
        /// <param name="process">supplied process</param>
        /// <returns></returns>
        public static Process? GetParentProcessWindows(Process process)
        {
            string query = $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            using (ManagementObjectCollection results = searcher.Get())
            {
                var managementObject = results.Cast<ManagementBaseObject>().FirstOrDefault();
                if (managementObject is not null)
                {
                    int? parentId = Convert.ToInt32(managementObject["ParentProcessId"]);
                    if (parentId is not null)
                    {
                        return Process.GetProcessById((int)parentId);
                    }
                }
            }
            return null;
        }
    }
}
