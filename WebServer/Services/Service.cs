using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Xml;

namespace WebServer.Services
{
    /// <summary>
    /// Concrete implementation of the IService interface.
    /// </summary>
    public class Service : IService
    {
        public string? ServiceName { get; set; }
        public string? ServiceDescription { get; set; }
        public string? AppIcon { get; set; }
        public string? WindowTitle { get; set; }
        public IService.OpStatus Status { get; set; }
        public List<DateTime>? UpTime { get; set; } = new List<DateTime>();
        public List<DateTime>? DownTime { get; set; } = new List<DateTime>();

        // New properties to store process-related details
        public int? ProcessId { get; set; }
        public string? ProcessName { get; set; }
        //public long? MemoryUsage { get; private set; }
        //public TimeSpan? ProcessorTime { get; private set; }

        public string? FileName { get; set; }
        public int? Port { get; set; }

        /// <summary>
        /// Constructor for the Service.
        /// </summary>
        /// <param name="name">Service Name</param>
        /// <param name="description">Service Description</param>
        public Service(string? name, string? description)
        {
            ServiceName = name;
            ServiceDescription = description;
            Status = IService.OpStatus.OFFLINE;
        }

        public string? GetName() => ServiceName;
        public string? GetDescription() => ServiceDescription;
        public string? GetAppIcon() => AppIcon;
        public string? GetWindowTitle() => WindowTitle;
        public IService.OpStatus GetStatus() => Status;
        public List<DateTime>? GetUpTime() => UpTime;
        public List<DateTime>? GetDownTime() => DownTime;
        public int? GetPort() => Port;
        public string? GetFileName() => FileName;

        /// <summary>
        /// Return this Poco object as Json string (serialized)
        /// </summary>
        /// <returns>Json object</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
