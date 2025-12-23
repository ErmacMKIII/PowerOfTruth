using System.Net;
using System.Net.Http.Headers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load settings from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Load settings from appsettings.json, LocalIPAddress, HttpPort, and HttpsPort
string localIP = builder.Configuration.GetValue<string>("LocalIPAddress") ?? "0.0.0.0";
string httpPort = builder.Configuration.GetValue<string>("HttpPort") ?? "5001";
string httpsPort = builder.Configuration.GetValue<string>("HttpsPort") ?? "44300";
builder.WebHost.UseUrls($"http://{localIP}:{httpPort}", $"https://{localIP}:{httpsPort}");

// Configure services
builder.Services.AddRazorPages();
var apiSettings = builder.Configuration.GetSection("ApiSettings");
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
});

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
