namespace WebServer.Services
{
    /*{
        "ServiceName": "Demolition Synergy",
        "ServiceDescription": "A game server process for Demolition Synergy",
        "AppIcon": "dsynergy-server.png",
        "ProcessName": "java",
        "ProcessId": null
    }*/
    /// <summary>
    /// Lookup to find process
    /// </summary>
    public class Lookup
    {
        /// <summary>
        /// Process Id on this PC
        /// </summary>
        public int? ProcessId { get; set; }
        /// <summary>
        /// Process Name on this PC
        /// </summary>
        public string[]? ProcessNames { get; set; }
        /// <summary>
        /// Service Name
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Service Description
        /// </summary>
        public string? Description { get; set; }
        /// <summary>
        /// Service App Icon
        /// </summary>
        public string? AppIcon { get; set; }
    }
}
