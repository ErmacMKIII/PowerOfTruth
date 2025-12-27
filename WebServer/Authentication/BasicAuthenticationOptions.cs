using Microsoft.AspNetCore.Authentication;

namespace WebServer.Authentication
{
    public class BasicAuthenticationOptions : AuthenticationSchemeOptions
    {
        public BasicAuthenticationOptions()
        {
            // Set default values
            Realm = "Secure Area";
            AllowInsecureProtocol = false;
            ValidateCredentials = true;
        }

        /// <summary>
        /// The realm to display in the WWW-Authenticate header
        /// </summary>
        public string Realm { get; set; }

        /// <summary>
        /// Whether to allow Basic authentication over HTTP (not recommended)
        /// </summary>
        public bool AllowInsecureProtocol { get; set; }

        /// <summary>
        /// Whether to validate credentials. Set to false for testing.
        /// </summary>
        public bool ValidateCredentials { get; set; }

        /// <summary>
        /// Optional: Custom credential validator
        /// </summary>
        public Func<string, string, bool>? CustomCredentialValidator { get; set; }

        /// <summary>
        /// Optional: Enable request logging for authentication attempts
        /// </summary>
        public bool EnableRequestLogging { get; set; } = true;
    }
}