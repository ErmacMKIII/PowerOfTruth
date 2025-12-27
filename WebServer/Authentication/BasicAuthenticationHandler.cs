using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;

namespace WebServer.Authentication
{
    /// <summary>
    /// Defines a static class to hold user credentials for Basic Authentication.
    /// </summary>
    public static class Users
    {
        public static readonly Dictionary<string, string> Credentials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Admin", "poweroftruth13667" }, // Please don't change this
        };

        /// <summary>
        /// Add or update a user credential
        /// </summary>
        public static void AddUser(string username, string password)
        {
            Credentials[username] = password;
        }

        /// <summary>
        /// Remove a user credential
        /// </summary>
        public static bool RemoveUser(string username)
        {
            return Credentials.Remove(username);
        }

        /// <summary>
        /// Validate user credentials
        /// </summary>
        public static bool ValidateUser(string username, string password)
        {
            return username != null &&
                   Credentials.TryGetValue(username, out var storedPassword) &&
                   storedPassword == password;
        }

        /// <summary>
        /// Get all usernames
        /// </summary>
        public static IEnumerable<string> GetAllUsers()
        {
            return Credentials.Keys;
        }
    }

    /// <summary>
    /// Enhanced Basic Authentication for ASP.NET Core with additional features.
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
    {
        private readonly ILogger<BasicAuthenticationHandler> _logger;

        public BasicAuthenticationHandler(
            IOptionsMonitor<BasicAuthenticationOptions> options,
            ILoggerFactory loggerFactory,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, loggerFactory, encoder, clock)
        {
            _logger = loggerFactory.CreateLogger<BasicAuthenticationHandler>();
        }

        /// <summary>
        /// Check if request is using HTTPS for security
        /// </summary>
        private bool IsSecureConnection()
        {
            return Request.IsHttps ||
                   Request.Headers["X-Forwarded-Proto"].ToString().Equals("https", StringComparison.OrdinalIgnoreCase);
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Security check: Basic Auth should only be used over HTTPS
            if (!IsSecureConnection() && !Options.AllowInsecureProtocol)
            {
                _logger.LogWarning("Basic authentication attempted over insecure HTTP connection");
                return Task.FromResult(AuthenticateResult.Fail("Basic authentication requires HTTPS"));
            }

            // Check if Authorization header exists
            if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                if (Options.EnableRequestLogging)
                {
                    _logger.LogDebug("Authorization header missing for request to {Path}", Request.Path);
                }
                return Task.FromResult(AuthenticateResult.Fail("Authorization header missing."));
            }

            var authHeader = authHeaderValues.ToString();
            if (string.IsNullOrEmpty(authHeader))
            {
                return Task.FromResult(AuthenticateResult.Fail("Authorization header is empty."));
            }

            // Log the attempt (masked for security)
            if (Options.EnableRequestLogging)
            {
                _logger.LogInformation("Authentication attempt from {RemoteIp} to {Path}",
                    Request.HttpContext.Connection.RemoteIpAddress, Request.Path);
            }

            // Parse and validate the authorization header
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var parsedHeader))
            {
                _logger.LogWarning("Invalid authorization header format from {RemoteIp}",
                    Request.HttpContext.Connection.RemoteIpAddress);
                return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header format."));
            }

            if (!parsedHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unsupported authentication scheme: {Scheme} from {RemoteIp}",
                    parsedHeader.Scheme, Request.HttpContext.Connection.RemoteIpAddress);
                return Task.FromResult(AuthenticateResult.Fail("Only Basic authentication is supported."));
            }

            if (string.IsNullOrEmpty(parsedHeader.Parameter))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing credentials in authorization header."));
            }

            try
            {
                var decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(parsedHeader.Parameter));
                var credentials = decodedCredentials.Split(':', 2);

                if (credentials.Length == 2)
                {
                    var username = credentials[0];
                    var password = credentials[1];

                    bool isValid = false;

                    if (!Options.ValidateCredentials)
                    {
                        // Skip validation for testing
                        isValid = true;
                        _logger.LogWarning("Credential validation disabled - accepting user: {Username}", username);
                    }
                    else if (Options.CustomCredentialValidator != null)
                    {
                        // Use custom validator if provided
                        isValid = Options.CustomCredentialValidator(username, password);
                    }
                    else
                    {
                        // Use default validation
                        isValid = Users.ValidateUser(username, password);
                    }

                    if (isValid)
                    {
                        // Create claims for the authenticated user
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.Name, username),
                            new Claim(ClaimTypes.NameIdentifier, username),
                            new Claim(ClaimTypes.AuthenticationMethod, "Basic"),
                            // Add timestamp claim
                            new Claim("AuthTime", DateTime.UtcNow.ToString("o")),
                            // Add IP claim
                            new Claim("RemoteIp", Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown")
                        };

                        // Check if user is Admin for role claim
                        if (username.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                        {
                            claims = claims.Append(new Claim(ClaimTypes.Role, "Administrator")).ToArray();
                        }

                        var identity = new ClaimsIdentity(claims, Scheme.Name);
                        var principal = new ClaimsPrincipal(identity);
                        var ticket = new AuthenticationTicket(principal, Scheme.Name);

                        // Log successful authentication
                        _logger.LogInformation("User {Username} authenticated successfully from {RemoteIp}",
                            username, Request.HttpContext.Connection.RemoteIpAddress);

                        return Task.FromResult(AuthenticateResult.Success(ticket));
                    }
                    else
                    {
                        // Log failed authentication attempt
                        _logger.LogWarning("Failed authentication attempt for user {Username} from {RemoteIp}",
                            username, Request.HttpContext.Connection.RemoteIpAddress);
                    }
                }
            }
            catch (FormatException)
            {
                _logger.LogWarning("Invalid base64 encoding in authorization header from {RemoteIp}",
                    Request.HttpContext.Connection.RemoteIpAddress);
                return Task.FromResult(AuthenticateResult.Fail("Invalid base64 encoding in authorization header."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing credentials from {RemoteIp}",
                    Request.HttpContext.Connection.RemoteIpAddress);
                return Task.FromResult(AuthenticateResult.Fail($"Error processing credentials: {ex.Message}"));
            }

            // If we get here, credentials were invalid
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            // This method is called when authorization fails
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "text/html";

            // Set WWW-Authenticate header with realm
            Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Options.Realm}\", charset=\"UTF-8\"";

            // Enhanced HTML response
            var responseHtml = $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Unauthorized Access</title>
                <style>
                    body {{
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        background-color: #f5f5f5;
                        margin: 0;
                        padding: 20px;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        min-height: 100vh;
                    }}
                    .container {{
                        background-color: white;
                        padding: 40px;
                        border-radius: 10px;
                        box-shadow: 0 4px 6px rgba(0,0,0,0.1);
                        max-width: 500px;
                        text-align: center;
                    }}
                    h1 {{
                        color: #d32f2f;
                        margin-bottom: 20px;
                    }}
                    p {{
                        color: #555;
                        margin-bottom: 15px;
                        line-height: 1.6;
                    }}
                    .code {{
                        background-color: #f8f9fa;
                        padding: 10px;
                        border-radius: 5px;
                        font-family: 'Courier New', monospace;
                        font-size: 14px;
                        margin: 15px 0;
                    }}
                    .info {{
                        background-color: #e3f2fd;
                        padding: 15px;
                        border-radius: 5px;
                        margin-top: 20px;
                        text-align: left;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h1>🔒 Unauthorized Access</h1>
                    <p>You are not authorized to access this resource.</p>
                    <p>Please provide valid Basic Authentication credentials.</p>
                    
                    <div class='code'>
                        Authorization: Basic &lt;base64-encoded-credentials&gt;
                    </div>                   
                    
                    <p><small>Request ID: {Context.TraceIdentifier}</small></p>
                </div>
            </body>
            </html>";

            await Response.WriteAsync(responseHtml);

            // Log the challenge response
            _logger.LogInformation("Challenge response sent for unauthorized request to {Path} from {RemoteIp}",
                Request.Path, Request.HttpContext.Connection.RemoteIpAddress);
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            // This method is called when user is authenticated but not authorized (forbidden)
            Response.StatusCode = StatusCodes.Status403Forbidden;
            Response.ContentType = "application/json";

            // Create a JSON response for API clients
            var forbiddenResponse = new
            {
                error = "Forbidden",
                message = "You are authenticated but do not have permission to access this resource.",
                path = Request.Path,
                timestamp = DateTime.UtcNow.ToString("o"),
                requestId = Context.TraceIdentifier
            };

            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(forbiddenResponse);

            // Log forbidden access
            var user = Context.User.Identity?.Name ?? "Unknown";
            _logger.LogWarning("Forbidden access attempt by user {User} to {Path} from {RemoteIp}",
                user, Request.Path, Request.HttpContext.Connection.RemoteIpAddress);

            return Response.WriteAsync(jsonResponse);
        }
    }
}