using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using WebServer.Authentication;

namespace WebServer.Authentication
{
    /// <summary>
    /// Defines a static class to hold user credentials for Basic Authentication.
    /// </summary>
    public static class Users
    {
        public static readonly Dictionary<string, string> Credentials = new Dictionary<string, string>()
        {
            { "Admin", "poweroftruth13667" }, // Please don't change this
        };
    }

    /// <summary>
    /// Complements Basic Authentication for ASP.NET Core.
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
    {
        public BasicAuthenticationHandler(IOptionsMonitor<BasicAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        private static bool CheckUserNamePassword(string? username, string? password)
        {
            return username != null && Users.Credentials.TryGetValue(username, out var storedPassword) && storedPassword == password;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Check if Authorization header exists
            if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                // Return failure without writing to response
                return Task.FromResult(AuthenticateResult.Fail("Authorization header missing."));
            }

            var authHeader = authHeaderValues.ToString();
            if (string.IsNullOrEmpty(authHeader))
            {
                return Task.FromResult(AuthenticateResult.Fail("Authorization header is empty."));
            }

            // Parse and validate the authorization header
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var parsedHeader))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header format."));
            }

            if (!parsedHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Only Basic authentication is supported."));
            }

            if (string.IsNullOrEmpty(parsedHeader.Parameter))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing credentials in authorization header."));
            }

            try
            {
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(parsedHeader.Parameter)).Split(':', 2);
                if (credentials.Length == 2)
                {
                    var username = credentials[0];
                    var password = credentials[1];

                    if (CheckUserNamePassword(username, password))
                    {
                        var identity = new GenericIdentity(username);
                        var principal = new GenericPrincipal(identity, null);
                        var ticket = new AuthenticationTicket(principal, "Basic");
                        return Task.FromResult(AuthenticateResult.Success(ticket));
                    }
                }
            }
            catch (FormatException)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid base64 encoding in authorization header."));
            }
            catch (Exception ex)
            {
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
            Response.Headers["WWW-Authenticate"] = "Basic realm=\"Secure Area\", charset=\"UTF-8\"";

            var responseHtml = @"
            <html>
                <head>
                    <title>Unauthorized Access</title>
                </head>
                <body>
                    <h1>Unauthorized Access</h1>
                    <p>You are not authorized to use this resource.</p>
                    <p>Please provide valid Basic Authentication credentials.</p>
                </body>
            </html>";

            await Response.WriteAsync(responseHtml);
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            // This method is called when user is authenticated but not authorized (forbidden)
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}