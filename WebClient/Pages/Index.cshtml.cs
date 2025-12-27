using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.NetworkInformation;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

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

            double totalUptime = 0.0;
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

            if (totalTime <= 0.0)
            {
                return 0.0; // Avoid division by zero or negative time
            }

            // Calculate availability as a percentage
            double availability = (totalUptime / totalTime) * 100.0;

            // Constrain the availability between 0 and 100
            availability = Math.Max(0.0, Math.Min(100.0, availability));

            return availability;
        }

    }

    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _configuration;
        protected List<Service>? _services = null;
        private readonly ILogger<IndexModel> _logger;

        // Handling error situations
        protected bool _isError = false;
        protected string? _errorMessage;
        protected string? _errorDetails;

        public List<Service>? Services { get => _services; }
        public bool IsError { get => _isError; }
        public string? ErrorMessage { get => _errorMessage; }
        public string? ErrorDetails { get => _errorDetails; }

        public IndexModel(IHttpClientFactory clientFactory, IConfiguration configuration, ILogger<IndexModel> logger)
        {
            _clientFactory = clientFactory;
            _configuration = configuration;
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
            _errorDetails = null;

            // Initialize _services to avoid null checks later
            _services = new List<Service>();

            // Get the HttpClient instance from the factory
            var client = _clientFactory.CreateClient("WebAPI");

            try
            {
                // API request
                Uri uri = new Uri("/api/status/check/services", UriKind.Relative);
                var response = await client.GetAsync(uri).ConfigureAwait(false);

                // Check if the API call succeeded
                if (!response.IsSuccessStatusCode)
                {
                    _isError = true;
                    _errorMessage = $"API Error: {response.StatusCode}";
                    _errorDetails = $"The server returned status code {(int)response.StatusCode} ({response.ReasonPhrase}). Please check if the WebServer is running and accessible.";
                    _logger.LogWarning($"API call failed: {response.StatusCode} - {response.ReasonPhrase}");
                    return;
                }

                // Read and process the response
                var responseData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(responseData))
                {
                    // Deserialize the JSON response
                    _services = JsonConvert.DeserializeObject<List<Service>>(
                        responseData,
                        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }
                    ) ?? new List<Service>();
                }
            }
            catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
            {
                // Handle timeout errors specifically
                _isError = true;
                _errorMessage = "Request Timeout";
                _errorDetails = "The request to the WebServer timed out. The server may be slow to respond or unreachable. Please check your network connection and server status.";
                _logger.LogError(tcEx, "Request timed out while connecting to WebServer.");
            }
            catch (TaskCanceledException tcEx)
            {
                // Handle general task cancellation (usually timeout)
                _isError = true;
                _errorMessage = "Request Timeout";

                var apiConfig = _configuration.GetSection("ApiSettings");
                var baseUrl = apiConfig.GetValue<string>("BaseUrl");
                var timeout = client?.Timeout.TotalSeconds ?? 120;

                _errorDetails = $"The request to {baseUrl} exceeded the timeout limit ({timeout} seconds). The server may be overloaded or not responding.";
                _logger.LogError(tcEx, "Request was cancelled (likely due to timeout).");
            }
            catch (HttpRequestException httpEx) when (httpEx.InnerException is System.Security.Authentication.AuthenticationException authEx)
            {
                // Handle SSL/TLS certificate validation errors
                _isError = true;
                _errorMessage = "Certificate Validation Failed";

                var apiConfig = _configuration.GetSection("ApiSettings");
                var baseUrl = apiConfig.GetValue<string>("BaseUrl");
                var certPath = apiConfig.GetValue<string>("CertificatePath");
                var bypassValidation = apiConfig.GetValue<bool>("BypassCertificateValidation");

                _errorDetails = $"Failed to establish a secure connection to {baseUrl}. ";

                if (bypassValidation)
                {
                    _errorDetails += "Certificate validation is bypassed but authentication still failed. ";
                }
                else if (!string.IsNullOrEmpty(certPath))
                {
                    _errorDetails += $"The server's certificate does not match the expected certificate at '{certPath}'. ";
                }
                else
                {
                    _errorDetails += "The server's certificate could not be validated. ";
                }

                _errorDetails += "Please verify the certificate configuration or enable 'BypassCertificateValidation' in development.";

                _logger.LogError(authEx, "Certificate validation failed: {Message}", authEx.Message);
            }
            catch (HttpRequestException httpEx) when (httpEx.Message.Contains("certificate") || httpEx.Message.Contains("SSL"))
            {
                // Handle other SSL/certificate-related errors
                _isError = true;
                _errorMessage = "SSL/Certificate Error";

                var apiConfig = _configuration.GetSection("ApiSettings");
                var baseUrl = apiConfig.GetValue<string>("BaseUrl");

                _errorDetails = $"An SSL/Certificate error occurred while connecting to {baseUrl}. ";
                _errorDetails += $"Error: {httpEx.Message}. ";
                _errorDetails += "Please check the certificate configuration in appsettings.json.";

                _logger.LogError(httpEx, "SSL/Certificate error occurred.");
            }
            catch (HttpRequestException httpEx)
            {
                // Handle other HTTP request errors (network failures, connection refused, etc.)
                _isError = true;
                _errorMessage = "Connection Error";

                var apiConfig = _configuration.GetSection("ApiSettings");
                var baseUrl = apiConfig.GetValue<string>("BaseUrl");

                _errorDetails = $"Failed to connect to the WebServer at {baseUrl}. ";

                if (httpEx.Message.Contains("Connection refused") || httpEx.Message.Contains("No connection could be made"))
                {
                    _errorDetails += "The server is not accepting connections. Please ensure the WebServer is running.";
                }
                else if (httpEx.Message.Contains("nodename nor servname provided"))
                {
                    _errorDetails += "The server address could not be resolved. Please check the BaseUrl in appsettings.json.";
                }
                else
                {
                    _errorDetails += $"Error: {httpEx.Message}";
                }

                _logger.LogError(httpEx, "HttpRequestException occurred while connecting to WebServer.");
            }
            catch (JsonException jsonEx)
            {
                // Handle JSON deserialization errors
                _isError = true;
                _errorMessage = "Data Format Error";
                _errorDetails = $"The server returned invalid data that could not be processed. Error: {jsonEx.Message}";
                _logger.LogError(jsonEx, "Failed to deserialize JSON response.");
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                _isError = true;
                _errorMessage = "Unexpected Error";
                _errorDetails = $"An unexpected error occurred: {ex.Message}. Please check the logs for more details.";
                _logger.LogError(ex, "An unexpected error occurred while fetching data.");
            }
        }
    }
}