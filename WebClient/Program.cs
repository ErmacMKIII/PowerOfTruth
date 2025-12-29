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
string localIP = builder.Configuration.GetValue<string>("LocalIP") ?? "0.0.0.0";
string httpPort = builder.Configuration.GetValue<string>("LocalHttpPort") ?? "5001";
string httpsPort = builder.Configuration.GetValue<string>("LocalHttpsPort") ?? "44300";
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
        // Set up custom certificate validation
        logger.LogInformation("Configuring custom certificate validation");

        // Simple validation: accept certificates with acceptable SSL policy errors
        handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
        {
            // If no certificate provided by server, reject
            if (certificate == null)
            {
                logger.LogError("Server did not provide a certificate");
                return false;
            }

            // Convert to X509Certificate2 for logging
            var serverCert = new X509Certificate2(certificate);

            logger.LogInformation("═══════════════════════════════════════════════════════════");
            logger.LogInformation("   Certificate Validation:");
            logger.LogInformation($"  Server Subject:    {serverCert.Subject}");
            logger.LogInformation($"  Server Issuer:     {serverCert.Issuer}");
            logger.LogInformation($"  Server Thumbprint: {serverCert.Thumbprint}");
            logger.LogInformation($"  Valid From:        {serverCert.NotBefore:yyyy-MM-dd HH:mm:ss}");
            logger.LogInformation($"  Valid To:          {serverCert.NotAfter:yyyy-MM-dd HH:mm:ss}");
            logger.LogInformation($"  SSL Policy Errors: {errors}");

            // Check if certificate is within valid date range
            var now = DateTime.Now;
            if (now < serverCert.NotBefore || now > serverCert.NotAfter)
            {
                logger.LogError($"   Certificate is not within valid date range");
                logger.LogError($"   Valid From: {serverCert.NotBefore}");
                logger.LogError($"   Valid To:   {serverCert.NotAfter}");
                logger.LogError($"   Current:    {now}");
                return false;
            }

            // Accept if no SSL policy errors
            if (errors == System.Net.Security.SslPolicyErrors.None)
            {
                logger.LogInformation("Certificate validation PASSED (no SSL errors)");
                logger.LogInformation("═══════════════════════════════════════════════════════════");
                return true;
            }

            // Accept RemoteCertificateNameMismatch (common when accessing by IP instead of hostname)
            if (errors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch)
            {
                logger.LogInformation("Certificate validation PASSED (name mismatch accepted for IP-based access)");
                logger.LogInformation("═══════════════════════════════════════════════════════════");
                return true;
            }

            // Accept RemoteCertificateChainErrors (common for self-signed certificates)
            if (errors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
            {
                logger.LogInformation("Certificate validation PASSED (chain errors accepted for self-signed certificates)");
                logger.LogInformation("═══════════════════════════════════════════════════════════");
                return true;
            }

            // Accept combination of NameMismatch and ChainErrors (self-signed cert accessed by IP)
            if (errors == (System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch |
                          System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors))
            {
                logger.LogInformation("Certificate validation PASSED (name mismatch + chain errors accepted)");
                logger.LogInformation("═══════════════════════════════════════════════════════════");
                return true;
            }

            // Reject other SSL policy errors
            logger.LogError($"Certificate validation FAILED: {errors}");
            logger.LogInformation("═══════════════════════════════════════════════════════════");
            return false;
        };

        logger.LogInformation("Custom certificate validation configured");
        logger.LogInformation("  Accepts: No errors, Name mismatch, Chain errors (self-signed)");
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