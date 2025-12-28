using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebServer.Services;

namespace WebServer.Controllers
{
    /// <summary>
    /// API controller for monitoring and reporting service status.
    /// Provides endpoints to check general API health and detailed service information.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        // Shared state across all controller instances
        public static List<Lookup>? _lookups = null;
        public static List<System.Diagnostics.Process>? _processes = null;
        public static List<Service>? _services = null;

        private readonly ILogger<StatusController> _logger;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the StatusController.
        /// Loads lookup configuration from AppServices.json on first instantiation.
        /// </summary>
        /// <param name="logger">Logger instance for logging controller activities</param>
        /// <param name="configuration">Application configuration for accessing settings</param>
        public StatusController(ILogger<StatusController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Load lookups only once (thread-safe initialization could be improved with lock)
            if (_lookups == null)
            {
                try
                {
                    var lookupFilePath = _configuration.GetValue<string>("LookupFilePath") ?? "AppServices.json";
                    _lookups = ServiceLookup.LoadLookupFromJson(lookupFilePath);
                    _logger.LogInformation($"Loaded {_lookups?.Count ?? 0} service lookup(s) from {lookupFilePath}");
                }
                catch (FileNotFoundException ex)
                {
                    _logger.LogError(ex, "Lookup configuration file not found");
                    _lookups = new List<Lookup>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse lookup configuration file");
                    _lookups = new List<Lookup>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error loading lookup configuration");
                    _lookups = new List<Lookup>();
                }
            }
        }

        /// <summary>
        /// Health check endpoint to verify the API is online.
        /// Returns an HTML page with a link to the detailed service status endpoint.
        /// </summary>
        /// <remarks>
        /// This endpoint does not require authentication and can be used for basic connectivity testing.
        /// </remarks>
        /// <returns>HTML content indicating the API is online</returns>
        /// <response code="200">API is online and responding</response>
        [HttpGet("check")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult CheckGeneralStatus()
        {
            _logger.LogInformation("General status check requested from {RemoteIp}",
                HttpContext.Connection.RemoteIpAddress);

            // Construct the HTML response with a hyperlink
            var responseHtml = @"
                <html>
                    <head>
                        <title>Service Status API</title>
                        <style>
                            body { font-family: Arial, sans-serif; margin: 40px; }
                            .status { color: green; font-weight: bold; }
                            a { color: #0066cc; text-decoration: none; }
                            a:hover { text-decoration: underline; }
                        </style>
                    </head>
                    <body>
                        <h1>Service Status API</h1>
                        <p class='status'>Online</p>
                        <p>The API is running and accepting requests.</p>
                        <p><a href='check/services'>View detailed service status (requires authentication)</a></p>
                    </body>
                </html>";

            return Content(responseHtml, "text/html");
        }

        /// <summary>
        /// Retrieves detailed status information for all monitored services.
        /// Returns JSON data containing service names, descriptions, operational status, uptime/downtime history, 
        /// and process information.
        /// </summary>
        /// <remarks>
        /// This endpoint requires Basic Authentication. The response includes:
        /// - Service operational status (STARTED, ONLINE, OFFLINE)
        /// - Process information (PID, process name, window title, file path, port)
        /// - Uptime and downtime history
        /// - Service availability metrics
        /// </remarks>
        /// <returns>JSON array of service status objects</returns>
        /// <response code="200">Successfully retrieved service status</response>
        /// <response code="400">Invalid configuration or lookup data</response>
        /// <response code="401">Authentication required</response>
        /// <response code="500">Internal server error occurred while processing services</response>
        [HttpGet("check/services")]
        [Authorize]
        [ProducesResponseType(typeof(List<Service>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult CheckServiceStatus()
        {
            _logger.LogInformation("Service status check requested by {User} from {RemoteIp}",
                User.Identity?.Name ?? "Unknown",
                HttpContext.Connection.RemoteIpAddress);

            // Validate lookup configuration
            if (_lookups == null || _lookups.Count == 0)
            {
                _logger.LogWarning("Service status check failed: No lookup configuration available");
                return BadRequest(new
                {
                    error = "Configuration Error",
                    message = "No service lookups configured. Please ensure AppServices.json exists and contains valid service definitions.",
                    timestamp = DateTime.UtcNow
                });
            }

            // Handle case where no processes have been discovered yet
            if (_processes == null || _processes.Count == 0)
            {
                _logger.LogInformation("No processes found. Returning existing service data (if any)");

                // Return existing services or empty list
                if (_services != null && _services.Count > 0)
                {
                    var servicesJson = JsonConvert.SerializeObject(_services, Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.Auto
                        });

                    return Ok(servicesJson);
                }

                // Return empty array with informational message
                _logger.LogInformation("No services to report");
                return Ok(JsonConvert.SerializeObject(new List<Service>(), Formatting.Indented));
            }

            // Update service status based on current process information
            try
            {
                _logger.LogDebug("Updating service status with {ProcessCount} process(es)", _processes.Count);

                bool servicesUpdated = ServiceLookup.CreateOrUpdateServices(
                    _processes,
                    _lookups,
                    ref _services);

                if (servicesUpdated)
                {
                    _logger.LogInformation("Service status updated successfully. Found {ServiceCount} service(s)",
                        _services?.Count ?? 0);
                }
                else
                {
                    _logger.LogDebug("No service status changes detected");
                }

                // Serialize services to JSON
                string servicesAsJson = JsonConvert.SerializeObject(_services, Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        NullValueHandling = NullValueHandling.Ignore
                    });

                // Log service status summary
                if (_services != null && _services.Count > 0)
                {
                    var statusSummary = _services
                        .GroupBy(s => s.Status)
                        .Select(g => $"{g.Key}: {g.Count()}")
                        .ToList();

                    _logger.LogInformation("Service status summary: {Summary}",
                        string.Join(", ", statusSummary));
                }

                return Ok(servicesAsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating service status");

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Internal Server Error",
                    message = "An error occurred while processing service status. Please check server logs for details.",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Diagnostic endpoint to check the current state of the controller's internal data.
        /// Useful for debugging and monitoring.
        /// </summary>
        /// <returns>JSON object with diagnostic information</returns>
        /// <response code="200">Diagnostic information retrieved successfully</response>
        [HttpGet("diagnostics")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetDiagnostics()
        {
            _logger.LogInformation("Diagnostics requested by {User}", User.Identity?.Name ?? "Unknown");

            var diagnostics = new
            {
                timestamp = DateTime.UtcNow,
                lookups = new
                {
                    count = _lookups?.Count ?? 0,
                    configured = _lookups?.Select(l => new
                    {
                        l.Name,
                        ProcessNames = l.ProcessNames,
                        l.ProcessId
                    }).ToList()
                },
                processes = new
                {
                    count = _processes?.Count ?? 0,
                    discovered = _processes?.Select(p => new
                    {
                        p.ProcessName,
                        p.Id,
                        HasExited = p.HasExited
                    }).ToList()
                },
                services = new
                {
                    count = _services?.Count ?? 0,
                    statuses = _services?.GroupBy(s => s.Status)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count())
                }
            };

            return Ok(diagnostics);
        }
    }
}