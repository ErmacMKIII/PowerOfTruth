namespace WebServer.Services
{
    /// <summary>
    /// Midnight Magic Service Interface
    /// Specifies details about the service
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Service Operational Status
        /// </summary>
        public enum OpStatus
        {
            /// <summary>
            /// Service is new and started recently
            /// </summary>
            STARTED,
            /// <summary>
            /// Service is operates online for some time
            /// </summary>
            ONLINE,
            /// <summary>
            /// Service is down. Needs inpesction (Optional)
            /// </summary>
            OFFLINE
        }

        /// <summary>
        /// Get Service Name
        /// </summary>
        /// <returns>Service Name</returns>
        public string? GetName();

        /// <summary>
        /// Get Window Title
        /// </summary>
        /// <returns>Window Title</returns>
        public string? GetWindowTitle();

        /// <summary>
        /// Get Service Description
        /// </summary>
        /// <returns>Service Description</returns>
        public string? GetDescription();

        /// <summary>
        /// Get App Icon (File Path)
        /// </summary>
        /// <returns>Get App Icon (File Path)</returns>
        public string? GetAppIcon();

        /// <summary>
        /// OpStatus of the service
        /// </summary>
        /// <returns>OpStatus of the service</returns>
        public OpStatus GetStatus();

        /// <summary>
        /// DateTime since the status changed from started to running
        /// </summary>
        /// <returns>DateTime since status changed to running</returns>
        public List<DateTime>? GetUpTime();

        /// <summary>
        /// DateTime since the status changed from running to stopped
        /// </summary>
        /// <returns>DateTime since status changed to stopped</returns>
        public List<DateTime>? GetDownTime();

        /// <summary>
        /// Return this Poco object as Json string (serialized)
        /// </summary>
        /// <returns>Json object</returns>
        public string ToJson();

        /// <summary>
        /// Get Main Module FileName which launched the process
        /// </summary>
        /// <returns></returns>
        public string? GetFileName();

        /// <summary>
        /// Get port associated with the service
        /// </summary>
        /// <returns></returns>
        public int? GetPort();
    }
}
