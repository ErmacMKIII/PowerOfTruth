using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;
using WebServer.Controllers;
using WebServer.Utils;

namespace WebServer.Services
{
    /// <summary>
    /// Provides methods for loading, finding, and managing services and processes
    /// based on lookup data and system process information.
    /// </summary>
    public static class ServiceLookup
    {
        /// <summary>
        /// Loads a list of <see cref="Lookup"/> objects from a JSON file.
        /// </summary>
        /// <param name="filePath">The path to the input JSON file.</param>
        /// <returns>A list of <see cref="Lookup"/> objects, or null if deserialization fails.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
        public static List<Lookup>? LoadLookupFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The process file '{filePath}' does not exist.");
            }

            return JsonConvert.DeserializeObject<List<Lookup>>(
                File.ReadAllText(filePath),
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }
            );
        }

        /// <summary>
        /// Finds and returns processes on the current machine based on the provided lookup information.
        /// </summary>
        /// <param name="lookups">A list of <see cref="Lookup"/> objects containing process search criteria.</param>
        /// <param name="resultProcesses">A reference to the list that will contain the found processes.</param>
        /// <returns>True if any processes are found; otherwise, false.</returns>
        public static bool FindProcesses(List<Lookup> lookups, ref List<Process>? resultProcesses)
        {
            resultProcesses ??= new List<Process>();
            bool isProcessFound = false;

            var allProcesses = Process.GetProcesses();
            var processLookup = allProcesses.GroupBy(p => p.ProcessName)
                                             .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var lookup in lookups)
            {
                List<Process>? matchedProcesses = null;

                if (lookup.ProcessId.HasValue)
                {
                    matchedProcesses = allProcesses.Where(p => p.Id == lookup.ProcessId).ToList();
                }
                else if (lookup.ProcessNames is not null)
                {
                    foreach (var processNamePattern in lookup.ProcessNames)
                    {
                        // Convert wildcard pattern to regex for case-insensitive matching
                        string regexPattern = "^" + Regex.Escape(processNamePattern)
                            .Replace("\\*", ".*")
                            .Replace("\\?", ".") + "$";

                        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

                        // Find all process names matching the pattern
                        var matchingNames = processLookup.Keys
                            .Where(key => regex.IsMatch(key))
                            .ToList();

                        // Collect all matching processes
                        matchedProcesses = new List<Process>();
                        foreach (var name in matchingNames)
                        {
                            if (processLookup.TryGetValue(name, out var processes))
                            {
                                matchedProcesses.AddRange(processes);
                            }
                        }

                        if (matchedProcesses.Count > 0)
                        {
                            break;
                        }
                    }
                }

                if (matchedProcesses != null && matchedProcesses.Count > 0)
                {
                    var targetProcess = matchedProcesses.FirstOrDefault();

                    if (targetProcess != null && !resultProcesses.Contains(targetProcess))
                    {
                        resultProcesses.Add(targetProcess);
                        isProcessFound = true;
                    }
                }
            }

            return isProcessFound;
        }

        /// <summary>
        /// Creates or updates services based on the provided processes and lookup data.
        /// Updates the status of services to reflect the current state of the system.
        /// </summary>
        /// <param name="processes">A list of running <see cref="Process"/> objects.</param>
        /// <param name="lookups">A list of <see cref="Lookup"/> objects containing service information.</param>
        /// <param name="services">A reference to the list of <see cref="Service"/> objects to update or create.</param>
        /// <returns>True if any services were created or updated; otherwise, false.</returns>
        public static bool CreateOrUpdateServices(
            List<Process> processes, List<Lookup> lookups, ref List<Service>? services)
        {
            services ??= new List<Service>();

            if (processes.Count == 0)
            {
                // Mark all existing services as offline
                return MarkAllServicesOffline(services);
            }

            // Build efficient lookup structures
            var lookupCache = BuildOptimizedLookupCache(lookups);
            var serviceDict = BuildServiceDictionary(services);
            var matchedServiceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool isUpdated = false;

            // First pass: Process all running processes and update/create services
            foreach (var process in processes)
            {
                var processName = process.ProcessName;
                if (process.HasExited)
                    continue;

                var lookup = FindMatchingLookup(processName, lookupCache);

                if (lookup == null || string.IsNullOrEmpty(lookup.Name))
                    continue;

                var serviceName = lookup.Name;
                matchedServiceNames.Add(serviceName);

                if (!serviceDict.TryGetValue(serviceName, out var service))
                {
                    // Create new service
                    service = CreateNewService(process, lookup);
                    services.Add(service);
                    serviceDict[serviceName] = service;
                    isUpdated = true;
                }
                else
                {
                    // Update existing service
                    isUpdated |= UpdateExistingService(service, process, lookup);
                }
            }

            // ---------------------------------------------------------------------------------------------
            // Mark services as offline when their process names don't have any matching running processes.
            // ---------------------------------------------------------------------------------------------

            // Second pass: Mark unmatched services as offline
            foreach (var service in services)
            {

                if (!matchedServiceNames.Contains(service.ServiceName ?? string.Empty) &&
                    service.Status != IService.OpStatus.OFFLINE)
                {
                    MarkServiceOffline(service);
                    isUpdated = true;
                }
            }

            return isUpdated;
        }

        /// <summary>
        /// Builds a dictionary mapping service names to <see cref="Service"/> instances.
        /// </summary>
        /// <param name="services">A list of <see cref="Service"/> objects.</param>
        /// <returns>A dictionary of service names to <see cref="Service"/> objects.</returns>
        private static Dictionary<string, Service> BuildServiceDictionary(List<Service> services)
        {
            var dict = new Dictionary<string, Service>(StringComparer.OrdinalIgnoreCase);
            foreach (var service in services)
            {
                if (service.ServiceName != null)
                {
                    dict[service.ServiceName] = service;
                }
            }
            return dict;
        }

        /// <summary>
        /// Builds optimized lookup cache structures for exact and wildcard process name matches.
        /// </summary>
        /// <param name="lookups">A list of <see cref="Lookup"/> objects.</param>
        /// <returns>
        /// A tuple containing a dictionary of exact matches and a list of wildcard match sets.
        /// </returns>
        private static (Dictionary<string, Lookup> exactMatches,
                       List<(HashSet<string> matchedProcesses, Lookup lookup)> wildcardMatches)
            BuildOptimizedLookupCache(List<Lookup> lookups)
        {
            var exactMatches = new Dictionary<string, Lookup>(StringComparer.OrdinalIgnoreCase);
            var wildcardMatches = new List<(HashSet<string> matchedProcesses, Lookup lookup)>();

            foreach (var lookup in lookups)
            {
                if (lookup.ProcessNames == null || lookup.ProcessNames.Count() == 0)
                    continue;

                foreach (var processPattern in lookup.ProcessNames)
                {
                    if (string.IsNullOrEmpty(processPattern))
                        continue;

                    if (processPattern.Contains('*') || processPattern.Contains('?'))
                    {
                        // For wildcard patterns, we'll track which processes match this pattern
                        wildcardMatches.Add((new HashSet<string>(StringComparer.OrdinalIgnoreCase), lookup));
                    }
                    else
                    {
                        // Exact match
                        exactMatches[processPattern] = lookup;
                    }
                }
            }

            return (exactMatches, wildcardMatches);
        }

        /// <summary>
        /// Finds a matching <see cref="Lookup"/> for a given process name using the provided lookup cache.
        /// </summary>
        /// <param name="processName">The process name to match.</param>
        /// <param name="lookupCache">The lookup cache containing exact and wildcard matches.</param>
        /// <returns>The matching <see cref="Lookup"/>, or null if no match is found.</returns>
        private static Lookup? FindMatchingLookup(string processName,
            (Dictionary<string, Lookup> exactMatches,
             List<(HashSet<string> matchedProcesses, Lookup lookup)> wildcardMatches) lookupCache)
        {
            // Try exact match first (fastest)
            if (lookupCache.exactMatches.TryGetValue(processName, out var exactLookup))
            {
                return exactLookup;
            }

            // Try wildcard matches
            foreach (var (matchedProcesses, lookup) in lookupCache.wildcardMatches)
            {
                // Check if we've already determined this process matches
                if (matchedProcesses.Contains(processName))
                {
                    return lookup;
                }

                // Check each pattern in the lookup
                if (lookup.ProcessNames != null)
                {
                    foreach (var pattern in lookup.ProcessNames)
                    {
                        if (string.IsNullOrEmpty(pattern))
                            continue;

                        if (IsWildcardMatch(processName, pattern))
                        {
                            matchedProcesses.Add(processName);
                            return lookup;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Determines if the input string matches the given wildcard pattern.
        /// Supports '*' and '?' wildcards.
        /// </summary>
        /// <param name="input">The input string to test.</param>
        /// <param name="pattern">The wildcard pattern to match against.</param>
        /// <returns>True if the input matches the pattern; otherwise, false.</returns>
        private static bool IsWildcardMatch(string input, string pattern)
        {
            // Fast path for exact match
            if (!pattern.Contains('*') && !pattern.Contains('?'))
            {
                return string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
            }

            // Simple wildcard implementation (more efficient than regex for common cases)
            int patternIndex = 0;
            int inputIndex = 0;
            int patternLength = pattern.Length;
            int inputLength = input.Length;

            while (patternIndex < patternLength && inputIndex < inputLength)
            {
                char patternChar = pattern[patternIndex];

                if (patternChar == '*')
                {
                    // Skip consecutive '*'
                    while (patternIndex < patternLength && pattern[patternIndex] == '*')
                        patternIndex++;

                    // If '*' is at the end, match everything remaining
                    if (patternIndex == patternLength)
                        return true;

                    // Find next match after '*'
                    char nextPatternChar = pattern[patternIndex];
                    while (inputIndex < inputLength &&
                           !CharsEqualIgnoreCase(input[inputIndex], nextPatternChar))
                        inputIndex++;

                    if (inputIndex == inputLength)
                        return false;
                }
                else if (patternChar == '?' || CharsEqualIgnoreCase(input[inputIndex], patternChar))
                {
                    patternIndex++;
                    inputIndex++;
                }
                else
                {
                    return false;
                }
            }

            // Skip any trailing '*'
            while (patternIndex < patternLength && pattern[patternIndex] == '*')
                patternIndex++;

            return patternIndex == patternLength && inputIndex == inputLength;
        }

        /// <summary>
        /// Compares two characters for equality, ignoring case.
        /// </summary>
        /// <param name="a">The first character.</param>
        /// <param name="b">The second character.</param>
        /// <returns>True if the characters are equal ignoring case; otherwise, false.</returns>
        private static bool CharsEqualIgnoreCase(char a, char b)
        {
            return char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
        }

        /// <summary>
        /// Updates an existing <see cref="Service"/> instance with process and lookup data.
        /// </summary>
        /// <param name="service">The service to update.</param>
        /// <param name="process">The process associated with the service.</param>
        /// <param name="lookup">The lookup data for the service.</param>
        /// <returns>True if the service was updated; otherwise, false.</returns>
        private static bool UpdateExistingService(Service service, Process process, Lookup lookup)
        {
            bool updated = false;

            if (service.Status == IService.OpStatus.OFFLINE)
            {
                service.ProcessId = process.Id;
                service.ProcessName = process.ProcessName;
                service.WindowTitle = process.MainWindowTitle;
                service.FileName = GetProcessFileNameSafely(process);
                service.Port = Utils.NetworkUtility.GetPortForProcess(process.Id);
                service.UpTime?.Add(process.StartTime);
                service.Status = IService.OpStatus.ONLINE;
                updated = true;
            }
            else if (service.Status != IService.OpStatus.ONLINE)
            {
                service.Status = IService.OpStatus.ONLINE;
                updated = true;
            }

            return updated;
        }

        /// <summary>
        /// Marks a <see cref="Service"/> as offline and updates its downtime.
        /// </summary>
        /// <param name="service">The service to mark as offline.</param>
        private static void MarkServiceOffline(Service service)
        {
            if (service.DownTime == null ||
                service.DownTime.Count == 0 ||
                service.DownTime.Last() <= service.UpTime?.LastOrDefault())
            {
                service.DownTime?.Add(DateTime.Now);
            }
            service.Status = IService.OpStatus.OFFLINE;
        }

        /// <summary>
        /// Marks all services in the provided list as offline.
        /// </summary>
        /// <param name="services">A list of <see cref="Service"/> objects.</param>
        /// <returns>True if any services were updated; otherwise, false.</returns>
        private static bool MarkAllServicesOffline(List<Service> services)
        {
            bool isUpdated = false;
            var now = DateTime.Now;

            foreach (var service in services)
            {
                if (service.Status != IService.OpStatus.OFFLINE)
                {
                    MarkServiceOffline(service);
                    isUpdated = true;
                }
            }

            return isUpdated;
        }

        // (Keep existing CreateNewService and GetProcessFileNameSafely methods from previous version)

        /// <summary>
        /// Safely retrieves the main module file name for a process.
        /// </summary>
        /// <param name="process">The process to query.</param>
        /// <returns>The file name of the process's main module, or null if access is denied or unavailable.</returns>
        private static string? GetProcessFileNameSafely(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                // Handle access denied or other exceptions
                return null;
            }
        }

        /// <summary>
        /// Creates a new <see cref="Service"/> instance from a process and lookup data.
        /// </summary>
        /// <param name="process">The process to associate with the service.</param>
        /// <param name="lookup">The lookup data for the service.</param>
        /// <returns>A new <see cref="Service"/> object.</returns>
        private static Service CreateNewService(Process process, Lookup lookup)
        {
            var service = new Service(lookup.Name, lookup.Description)
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                WindowTitle = process.MainWindowTitle,
                Status = IService.OpStatus.STARTED,
                FileName = GetProcessFileNameSafely(process),
                Port = Utils.NetworkUtility.GetPortForProcess(process.Id),
                AppIcon = lookup.AppIcon
            };

            service.UpTime?.Add(process.StartTime);
            return service;
        }

        /// <summary>
        /// Marks a service as offline and updates the isUpdated flag.
        /// </summary>
        /// <param name="service">The service to mark as offline.</param>
        /// <param name="isUpdated">A flag indicating if the service was updated.</param>
        /// <returns>True if the service was updated; otherwise, false.</returns>
        private static bool MarkOfflineService(Service service,
                                               bool isUpdated)
        {
            var now = DateTime.Now;

            if (service.Status != IService.OpStatus.OFFLINE)
            {
                // Only add downtime if we haven't already marked it
                if (service.DownTime == null ||
                    service.DownTime.Count == 0 ||
                    service.DownTime.Last() <= service.UpTime?.LastOrDefault())
                {
                    service.DownTime?.Add(now);
                }
                service.Status = IService.OpStatus.OFFLINE;
                isUpdated = true;
            }

            return isUpdated;
        }

        /// <summary>
        /// Builds a lookup cache for exact and wildcard process name matches using regular expressions.
        /// </summary>
        /// <param name="lookups">A list of <see cref="Lookup"/> objects.</param>
        /// <returns>
        /// A tuple containing a dictionary of exact matches and a list of wildcard regex matches.
        /// </returns>
        private static (Dictionary<string, Lookup> exactMatches,
                        List<(Regex regex, Lookup lookup)> wildcardMatches)
            BuildLookupCache(List<Lookup> lookups)
        {
            var exactMatches = new Dictionary<string, Lookup>(StringComparer.OrdinalIgnoreCase);
            var wildcardMatches = new List<(Regex regex, Lookup lookup)>();

            foreach (var lookup in lookups)
            {
                if (lookup.ProcessNames == null) continue;

                foreach (var processName in lookup.ProcessNames)
                {
                    if (string.IsNullOrEmpty(processName)) continue;

                    // Check if it's a wildcard pattern
                    if (processName.Contains('*') || processName.Contains('?'))
                    {
                        // Convert wildcard to regex
                        string regexPattern = "^" + Regex.Escape(processName)
                            .Replace("\\*", ".*")
                            .Replace("\\?", ".") + "$";

                        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        wildcardMatches.Add((regex, lookup));
                    }
                    else
                    {
                        // Exact match - store in dictionary
                        exactMatches[processName] = lookup;
                    }
                }
            }

            return (exactMatches, wildcardMatches);
        }

        /// <summary>
        /// Finds a matching <see cref="Lookup"/> for a given process name using a regex-based lookup cache.
        /// </summary>
        /// <param name="processName">The process name to match.</param>
        /// <param name="lookupCache">The lookup cache containing exact and wildcard regex matches.</param>
        /// <returns>The matching <see cref="Lookup"/>, or null if no match is found.</returns>
        private static Lookup? FindMatchingLookup(string processName,
            (Dictionary<string, Lookup> exactMatches,
             List<(Regex regex, Lookup lookup)> wildcardMatches) lookupCache)
        {
            // First try exact match (fastest)
            if (lookupCache.exactMatches.TryGetValue(processName, out var exactLookup))
            {
                return exactLookup;
            }

            // Then try wildcard matches
            foreach (var (regex, lookup) in lookupCache.wildcardMatches)
            {
                if (regex.IsMatch(processName))
                {
                    return lookup;
                }
            }

            return null;
        }

        /// <summary>
        /// Marks all services as offline if no processes are found.
        /// </summary>
        /// <param name="services">A list of <see cref="Service"/> objects.</param>
        /// <param name="isUpdated">A flag indicating if any service was updated.</param>
        /// <returns>True if any services were updated; otherwise, false.</returns>
        private static bool UpdateOfflineServices(List<Service> services, bool isUpdated)
        {
            var now = DateTime.Now;

            foreach (var service in services)
            {
                if (service.Status != IService.OpStatus.OFFLINE)
                {
                    service.Status = IService.OpStatus.OFFLINE;

                    // Add downtime if needed
                    if (service.DownTime == null ||
                        service.DownTime.Count == 0 ||
                        service.DownTime.Last() <= service.UpTime?.LastOrDefault())
                    {
                        service.DownTime?.Add(now);
                    }

                    isUpdated = true;
                }
            }

            return isUpdated;
        }
    }
}