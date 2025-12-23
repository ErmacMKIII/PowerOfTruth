namespace WebServer.Utils
{
    using System.Diagnostics;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Network utility for locating ports in use
    /// </summary>
    public static class NetworkUtility
    {
        /// <summary>
        /// Get Port of the process (most likely from the OS)
        /// </summary>
        /// <param name="processId">Process Id</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">if process is not found (by Id)</exception>
        public static int? GetPortForProcess(int processId)
        {
            var process = Process.GetProcessById(processId);

            if (process == null)
            {
                throw new InvalidOperationException("Process not found.");
            }

            var netstatOutput = RunNetstat();

            return ParsePortFromNetstatOutput(netstatOutput, processId);
        }

        /// <summary>
        /// Run Netstat to get the networking info as string about the process
        /// </summary>
        /// <returns></returns>
        private static string RunNetstat()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-a -n -o",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output;
        }

        /// <summary>
        /// Return the port from the string got as Netstat.
        /// </summary>
        /// <param name="netstatOutput"></param>
        /// <param name="processId"></param>
        /// <returns>null if port not found or != null if port is found</returns>
        private static int? ParsePortFromNetstatOutput(string netstatOutput, int processId)
        {
            var lines = netstatOutput.Split('\n');

            foreach (var line in lines)
            {
                // Example output format:
                //  TCP    0.0.0.0:80           0.0.0.0:0              LISTENING       1234

                if (line.Contains(processId.ToString()))
                {
                    var match = Regex.Match(line, @"\s+(\d+\.\d+\.\d+\.\d+):(\d+)\s+");

                    if (match.Success && match.Groups.Count > 2)
                    {
                        return int.Parse(match.Groups[2].Value);
                    }
                }
            }

            return null; // Port not found
        }
    }

}
