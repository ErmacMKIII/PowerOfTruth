using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                return AuthenticateResult.Fail("Authorization header missing.");
            }

            var authHeader = AuthenticationHeaderValue.Parse(authHeaderValues.ToString());
            if (authHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) && authHeader.Parameter != null)
            {
                try
                {
                    var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter)).Split(':', 2);
                    if (credentials.Length == 2)
                    {
                        var username = credentials[0];
                        var password = credentials[1];

                        if (CheckUserNamePassword(username, password))
                        {
                            var identity = new GenericIdentity(username);
                            var principal = new GenericPrincipal(identity, null);
                            return AuthenticateResult.Success(new AuthenticationTicket(principal, "Basic"));
                        }
                    }
                }
                catch (FormatException)
                {
                    return AuthenticateResult.Fail("Invalid authorization header format.");
                }
            }

            // Handle unauthorized access by returning an HTML response
            Context.Response.ContentType = "text/html";
            Context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            var responseHtml = @"
            <html>
                <body>
                    <h1>Unauthorized Access</h1>
                    <p>You are not authorized to use this resource.</p>
                </body>
            </html>";

            await Context.Response.WriteAsync(responseHtml);

            // Return a failed authentication result after writing the response
            return AuthenticateResult.Fail("Unauthorized access!");
        }
    }
}
