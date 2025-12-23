using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.NetworkInformation;
using Newtonsoft.Json;
using System;

namespace WebClient.Pages
{
    public class Service
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

        public string? ServiceName { get; set; }
        public string? ServiceDescription { get; set; }
        public string? AppIcon { get; set; }
        public string? WindowTitle { get; set; }
        public Service.OpStatus Status { get; set; }
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
        /// Get Relative Up Time (Now since UpTime delta)
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetRelativeOperationalTime()
        {
            if (this.UpTime is not null && this.Status != OpStatus.OFFLINE)
            {
                TimeSpan relative = DateTime.Now - this.UpTime.LastOrDefault();

                return relative;
            } 
            else if (this.DownTime is not null && this.Status == OpStatus.OFFLINE)
            {
                TimeSpan relative = DateTime.Now - this.DownTime.LastOrDefault();

                return relative;
            }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Calculates the general availability of the service as a percentage.
        /// </summary>
        /// <returns>Availability percentage (0-100)</returns>
        public double CalculateAvailability()
        {
            if (UpTime == null || DownTime == null || UpTime.Count == 0)
            {
                return 0.0;
            }

            // Ensure DownTime is not empty and covers the same period as UpTime
            if (DownTime.Count == 0 || DownTime.Count < UpTime.Count)
            {
                // Assuming the service is currently up if DownTime is missing
                DownTime.Add(DateTime.Now);
            }

            double totalUptime = 0;
            DateTime? lastDownTime = null;

            for (int i = 0; i < UpTime.Count; i++)
            {
                DateTime up = UpTime[i];
                DateTime down;

                if (i < DownTime.Count)
                {
                    down = DownTime[i];
                }
                else
                {
                    down = DateTime.Now;
                }

                if (down < up)
                {
                    // Correcting potential data error: Skip this pair
                    continue;
                }

                // Calculate the uptime for this period
                totalUptime += (down - up).TotalSeconds;
                lastDownTime = down;
            }

            // Calculate total time from the first UpTime to the current time or last downtime
            DateTime endTime = lastDownTime.HasValue ? lastDownTime.Value : DateTime.Now;
            double totalTime = (endTime - UpTime.First()).TotalSeconds;

            if (totalTime <= 0)
            {
                return 0.0; // Avoid division by zero or negative time
            }

            // Calculate availability as a percentage
            double availability = (totalUptime / totalTime) * 100;

            // Constrain the availability between 0 and 100
            availability = Math.Max(0.0, Math.Min(100.0, availability));

            return availability;
        }

    }
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _clientFactory;
        protected List<Service>? _services = null;
        private readonly ILogger<IndexModel> _logger;
        // Handling error situations
        protected bool _isError = false;
        protected string? _errorMessage;

        public List<Service>? Services { get => _services; }

        public bool IsError { get => _isError; }
        public string? ErrorMessage { get => _errorMessage; }

        public IndexModel(IHttpClientFactory clientFactory, ILogger<IndexModel> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        }

        /// <summary>
        /// What to do on loading the page.
        /// </summary>
        /// <returns></returns>
        public async Task OnGet()
        {
            _isError = false;
            _errorMessage = null;

            // Initialize _services to avoid null checks later
            _services = new List<Service>();

            try
            {
                // Get the HttpClient instance from the factory
                var client = _clientFactory.CreateClient("WebAPI");

                // API request
                Uri uri = new Uri("/api/status/check/services", UriKind.Relative);
                var response = await client.GetAsync(uri).ConfigureAwait(false);

                // Check if the API call succeeded
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"API call failed: {response.StatusCode} - {response.ReasonPhrase}");
                    return;  // Exit early, no further processing required
                }

                // Read and process the response
                var responseData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(responseData))
                {
                    // Deserialize the JSON response
                    _services = JsonConvert.DeserializeObject<List<Service>>(
                        responseData,
                        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }
                    ) ?? new List<Service>();  // Ensure _services is always a valid list
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Handle specific HTTP request errors (network failures, etc.)
                _isError = true;
                _errorMessage = "A network error occurred while fetching data.";
                _logger.LogError(httpEx, "HttpRequestException occurred.");
            }
            catch (Exception ex)
            {
                // Handle other generic errors
                _isError = true;
                _errorMessage = $"An unexpected error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while fetching data.");
            }
        }

    }
}
