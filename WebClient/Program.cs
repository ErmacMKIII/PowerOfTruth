using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// LOAD CONFIGURATION
// ============================================================================

// Load settings from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// ============================================================================
// NETWORK CONFIGURATION
// ============================================================================

// Load settings from appsettings.json, LocalIPAddress, HttpPort, and HttpsPort
string localIP = builder.Configuration.GetValue<string>("LocalIPAddress") ?? "0.0.0.0";
string httpPort = builder.Configuration.GetValue<string>("HttpPort") ?? "5001";
string httpsPort = builder.Configuration.GetValue<string>("HttpsPort") ?? "44300";
builder.WebHost.UseUrls($"http://{localIP}:{httpPort}", $"https://{localIP}:{httpsPort}");

// ============================================================================
// SERVICE CONFIGURATION
// ============================================================================

// Configure services
builder.Services.AddRazorPages();
var apiSettings = builder.Configuration.GetSection("ApiSettings");

// ============================================================================
// HTTPCLIENT CONFIGURATION
// ============================================================================

// Configure HttpClient with Basic Authentication and Certificate Validation
builder.Services.AddHttpClient("WebAPI", client =>
{
    client.BaseAddress = new Uri(apiSettings.GetValue<string>("BaseUrl"));
}).ConfigureHttpClient((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSection("ApiSettings");
    var username = config.GetValue<string>("Username");
    var password = config.GetValue<string>("Password");
    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    client.Timeout = TimeSpan.FromSeconds(120d); // Set timeout to 120 seconds
}).ConfigurePrimaryHttpMessageHandler((serviceProvider) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var apiConfig = config.GetSection("ApiSettings");
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("CertificateValidation");

    var handler = new HttpClientHandler();

    bool bypassCertValidation = apiConfig.GetValue<bool>("BypassCertificateValidation");

    // Bypass certificate validation for development environment
    if (bypassCertValidation && builder.Environment.IsDevelopment())
    {
        logger.LogWarning("Certificate validation is BYPASSED for development environment");
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    else
    {
        // Load and validate with specific certificate
        var certPath = apiConfig["CertificatePath"];

        if (!string.IsNullOrEmpty(certPath))
        {
            // Handle relative paths
            if (!Path.IsPathRooted(certPath))
            {
                certPath = Path.Combine(Directory.GetCurrentDirectory(), certPath);
            }

            if (File.Exists(certPath))
            {
                logger.LogInformation($"Loading certificate from: {certPath}");

                try
                {
                    // Load the expected certificate for validation
                    var expectedCert = new X509Certificate2(certPath);
                    logger.LogInformation($"Expected certificate loaded - Subject: {expectedCert.Subject}, Thumbprint: {expectedCert.Thumbprint}");

                    // Set up custom certificate validation
                    handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
                    {
                        // If no certificate provided by server, reject
                        if (certificate == null)
                        {
                            logger.LogError("Server did not provide a certificate");
                            return false;
                        }

                        // Convert to X509Certificate2 for better comparison
                        var serverCert = new X509Certificate2(certificate);

                        logger.LogInformation($"Server certificate - Subject: {serverCert.Subject}, Issuer: {serverCert.Issuer}");
                        logger.LogInformation($"Server certificate Thumbprint: {serverCert.Thumbprint}");
                        logger.LogInformation($"SSL Policy Errors: {errors}");

                        // Compare certificates by thumbprint (most reliable method)
                        bool thumbprintMatch = serverCert.Thumbprint.Equals(expectedCert.Thumbprint, StringComparison.OrdinalIgnoreCase);

                        if (!thumbprintMatch)
                        {
                            logger.LogError($"Certificate thumbprint mismatch. Expected: {expectedCert.Thumbprint}, Got: {serverCert.Thumbprint}");
                            return false;
                        }

                        // Check for SSL policy errors
                        if (errors != System.Net.Security.SslPolicyErrors.None)
                        {
                            logger.LogWarning($"SSL Policy Errors detected: {errors}");

                            // Allow RemoteCertificateNameMismatch and RemoteCertificateChainErrors for self-signed certificates
                            // but only if the thumbprint matches
                            if (thumbprintMatch)
                            {
                                // Accept self-signed certificates with name mismatches if thumbprint is correct
                                if (errors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors))
                                {
                                    logger.LogInformation("Accepting certificate with chain errors due to thumbprint match (likely self-signed)");
                                }
                                if (errors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch))
                                {
                                    logger.LogInformation("Accepting certificate with name mismatch due to thumbprint match");
                                }
                                return true;
                            }

                            logger.LogError("SSL Policy Errors present and thumbprint does not match");
                            return false;
                        }

                        logger.LogInformation("Certificate validation successful");
                        return true;
                    };
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to load certificate from path: {certPath}");
                    throw;
                }
            }
            else
            {
                logger.LogError($"Certificate file not found at: {certPath}");
                throw new FileNotFoundException($"Certificate file not found at: {certPath}");
            }
        }
        else
        {
            logger.LogWarning("No certificate path configured. Using default certificate validation.");
        }
    }

    return handler;
});

// ============================================================================
// BUILD APPLICATION
// ============================================================================

var app = builder.Build();

// ============================================================================
// LOG STARTUP INFORMATION
// ============================================================================

var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║          WEBCLIENT STARTUP CONFIGURATION                  ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");
logger.LogInformation($"Environment: {app.Environment.EnvironmentName}");
logger.LogInformation($"Application: {app.Environment.ApplicationName}");
logger.LogInformation($"Content Root: {app.Environment.ContentRootPath}");
logger.LogInformation("");
logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║              LISTENING ADDRESSES                          ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");
logger.LogInformation($"HTTP:  http://{localIP}:{httpPort}");
logger.LogInformation($"HTTPS: https://{localIP}:{httpsPort}");

// Display accessible URLs
if (localIP == "0.0.0.0" || localIP == "*")
{
    logger.LogInformation("");
    logger.LogInformation("Accessible URLs:");
    logger.LogInformation($"  - http://localhost:{httpPort}");
    logger.LogInformation($"  - https://localhost:{httpsPort}");
    try
    {
        var hostName = System.Net.Dns.GetHostName();
        var hostEntry = System.Net.Dns.GetHostEntry(hostName);
        foreach (var ip in hostEntry.AddressList.Where(addr => addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
        {
            logger.LogInformation($"  - http://{ip}:{httpPort}");
            logger.LogInformation($"  - https://{ip}:{httpsPort}");
        }
    }
    catch
    {
        // Ignore DNS resolution errors
    }
}

logger.LogInformation("");
logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║              API CONNECTION SETTINGS                      ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");
logger.LogInformation($"API Base URL: {apiSettings.GetValue<string>("BaseUrl")}");
logger.LogInformation($"Username: {apiSettings.GetValue<string>("Username")}");
logger.LogInformation($"Timeout: 120 seconds");
logger.LogInformation($"Certificate Validation Bypass: {apiSettings.GetValue<bool>("BypassCertificateValidation")}");

var certPath = apiSettings.GetValue<string>("CertificatePath");
if (!string.IsNullOrEmpty(certPath))
{
    logger.LogInformation($"Certificate Path: {certPath}");
}

logger.LogInformation("═══════════════════════════════════════════════════════════");

// ============================================================================
// MIDDLEWARE PIPELINE CONFIGURATION
// ============================================================================

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    logger.LogInformation("Production middleware enabled (Exception Handler, HSTS)");
}
else
{
    logger.LogInformation("Development mode enabled");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

logger.LogInformation("Middleware pipeline configured");
logger.LogInformation("WebClient is ready to accept requests");
logger.LogInformation("═══════════════════════════════════════════════════════════");

// ============================================================================
// START APPLICATION
// ============================================================================

app.Run();