using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography.X509Certificates;
using WebServer.Authentication;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// SERVICE CONFIGURATION
// ============================================================================

// Register MVC controllers as services
builder.Services.AddControllers();

// ============================================================================
// LOAD NETWORK CONFIGURATION FROM APPSETTINGS
// ============================================================================

// Retrieve the local IP address to bind to (defaults to 0.0.0.0 to accept all interfaces)
string localIP = builder.Configuration.GetValue<string>("LocalIPAddress") ?? "0.0.0.0";

// Retrieve HTTP port from configuration (defaults to 5000)
string httpPort = builder.Configuration.GetValue<string>("HttpPort") ?? "5000";

// Retrieve HTTPS port from configuration (defaults to 44343)
string httpsPort = builder.Configuration.GetValue<string>("HttpsPort") ?? "44343";

// ============================================================================
// CERTIFICATE CONFIGURATION
// ============================================================================

// Configure HTTPS certificate from appsettings.json
ConfigureCertificateFromSettings(builder);

// Configure Kestrel to listen on both HTTP and HTTPS with the specified IP and ports
builder.WebHost.UseUrls($"http://{localIP}:{httpPort}", $"https://{localIP}:{httpsPort}");

// ============================================================================
// CROSS-ORIGIN RESOURCE SHARING (CORS) CONFIGURATION
// ============================================================================

// Add CORS policy that allows requests from any origin with any method and headers
// Note: This is permissive and should be restricted in production environments
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============================================================================
// AUTHENTICATION CONFIGURATION
// ============================================================================

// Register Basic Authentication scheme using custom BasicAuthenticationHandler
builder.Services.AddAuthentication("Basic")
    .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>("Basic", null);

// ============================================================================
// BACKGROUND SERVICE CONFIGURATION
// ============================================================================

// Retrieve the refresh interval from configuration (defaults to 30 seconds)
double interval = builder.Configuration.GetValue<double?>("RefreshTimer") ?? 30.0;

// Register ServiceUpdater as a hosted background service that runs periodically
builder.Services.AddHostedService<WebServer.Helper.ServiceUpdater>(sp =>
{
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WebServer.Helper.ServiceUpdater>>();
    var serviceUpdater = new WebServer.Helper.ServiceUpdater(sp, logger);
    serviceUpdater.Interval = TimeSpan.FromSeconds(interval);
    return serviceUpdater;
});

// ============================================================================
// BUILD AND CONFIGURE APPLICATION MIDDLEWARE PIPELINE
// ============================================================================

var app = builder.Build();

// Enable the CORS policy defined above
app.UseCors("AllowAll");

// Enable routing middleware to match incoming requests to endpoints
app.UseRouting();

// Enable authentication middleware to validate user credentials
app.UseAuthentication();

// Enable authorization middleware to enforce access control policies
app.UseAuthorization();

// Map controller endpoints to handle HTTP requests
app.MapControllers();

// Start the web server and listen for incoming requests
app.Run();

// ============================================================================
// CERTIFICATE CONFIGURATION HELPER METHODS
// ============================================================================

/// <summary>
/// Configures the HTTPS certificate from appsettings.json.
/// Loads certificate from file path if configured, otherwise uses ASP.NET Core development certificate.
/// </summary>
/// <param name="builder">The WebApplicationBuilder instance used to access configuration and environment settings</param>
static void ConfigureCertificateFromSettings(WebApplicationBuilder builder)
{
    var config = builder.Configuration;
    var logger = LoggerFactory.Create(logging => logging.AddConsole())
        .CreateLogger("CertificateConfig");

    var environment = builder.Environment.EnvironmentName;
    var certPath = config["CertificateSettings:Path"];

    logger.LogInformation($"Configuring HTTPS certificate for {environment} environment");

    // If certificate path is configured, use file-based certificate
    if (!string.IsNullOrEmpty(certPath))
    {
        ConfigureCertificateFromFile(config, logger);
    }
    else
    {
        // Use ASP.NET Core's default development certificate
        logger.LogInformation("No certificate path configured. Using default ASP.NET Core development certificate");
        VerifyDevelopmentCertificate(logger);
    }
}

/// <summary>
/// Loads an X.509 certificate from a file (PFX format) and configures it for HTTPS.
/// Supports both relative and absolute file paths.
/// </summary>
/// <param name="config">The application configuration containing the certificate file path and password</param>
/// <param name="logger">Logger instance for logging configuration details and errors</param>
static void ConfigureCertificateFromFile(IConfiguration config, ILogger logger)
{
    var certPath = config["CertificateSettings:Path"];
    var certPassword = config["CertificateSettings:Password"];

    // Convert relative paths to absolute paths based on the current working directory
    if (!Path.IsPathRooted(certPath))
    {
        certPath = Path.Combine(Directory.GetCurrentDirectory(), certPath);
    }

    // Verify that the certificate file exists
    if (!File.Exists(certPath))
    {
        logger.LogError($"Certificate file not found: {certPath}");
        throw new FileNotFoundException($"Certificate file not found: {certPath}");
    }

    try
    {
        logger.LogInformation($"Loading certificate from file: {certPath}");

        // Load the certificate with or without password
        var certificate = string.IsNullOrEmpty(certPassword)
            ? new X509Certificate2(certPath)
            : new X509Certificate2(certPath, certPassword);

        // Log certificate details for verification
        logger.LogInformation($"Certificate loaded - Subject: {certificate.Subject}");
        logger.LogInformation($"Certificate Thumbprint: {certificate.Thumbprint}");
        logger.LogInformation($"Valid from: {certificate.NotBefore} to {certificate.NotAfter}");

        // Configure Kestrel to use the certificate
        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path", certPath);

        if (!string.IsNullOrEmpty(certPassword))
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password", certPassword);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Error loading certificate from file: {certPath}");
        throw;
    }
}

/// <summary>
/// Verifies that the default ASP.NET Core development certificate exists.
/// </summary>
/// <param name="logger">Logger instance for logging certificate discovery details</param>
static void VerifyDevelopmentCertificate(ILogger logger)
{
    try
    {
        // Check if development certificate exists for localhost
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var devCerts = store.Certificates.Find(
            X509FindType.FindBySubjectName,
            "localhost",
            validOnly: false);

        // Log the number and details of development certificates found
        if (devCerts.Count > 0)
        {
            logger.LogInformation($"Found {devCerts.Count} development certificate(s) for localhost");
            foreach (var cert in devCerts)
            {
                logger.LogInformation($"  - {cert.Subject} (Expires: {cert.NotAfter})");
            }
        }
        else
        {
            logger.LogWarning("No development certificate found. Run 'dotnet dev-certs https --trust' to create one.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error verifying development certificate");
    }
}