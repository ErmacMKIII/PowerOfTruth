using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebServer.Authentication; // Corrected the namespace to match the handler implementation

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers(); // Adds services required for controllers

// Load settings from appsettings.json, LocalIPAddress, HttpPort, and HttpsPort
string localIP = builder.Configuration.GetValue<string>("LocalIPAddress") ?? "0.0.0.0";
string httpPort = builder.Configuration.GetValue<string>("HttpPort") ?? "5000";
string httpsPort = builder.Configuration.GetValue<string>("HttpsPort") ?? "44343";
builder.WebHost.UseUrls($"http://{localIP}:{httpPort}", $"https://{localIP}:{httpsPort}");

// Optional: Add CORS policies if needed
// In the WebServer Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin() // You can also specify origins using .WithOrigins("http://localhost:7249") or similar
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add authentication (Basic Authentication in this case)
builder.Services.AddAuthentication("Basic")
    .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>("Basic", null);

// Register the background service
double interval = builder.Configuration.GetValue<double?>("RefreshTimer") ?? 30.0;
builder.Services.AddHostedService<WebServer.Helper.ServiceUpdater>(sp =>
{
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WebServer.Helper.ServiceUpdater>>();
    var serviceUpdater = new WebServer.Helper.ServiceUpdater(sp, logger);
    serviceUpdater.Interval = TimeSpan.FromSeconds(interval);
    return serviceUpdater;
});

// Configure Kestrel server to listen on both HTTP and HTTPS
//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.ListenLocalhost(5007); // HTTP
//    options.ListenLocalhost(7279, listenOptions => listenOptions.UseHttps("","")); // HTTPS
//});

var app = builder.Build();

// Optional: Use CORS if configured
app.UseCors("AllowAll");

app.UseRouting();

// Ensure authentication and authorization are used in the pipeline
app.UseAuthentication();
app.UseAuthorization();

// Map controllers to endpoints
app.MapControllers();

app.Run();
