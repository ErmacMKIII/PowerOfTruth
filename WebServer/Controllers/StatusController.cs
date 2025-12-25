using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebServer.Services;

[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase
{
    public static List<Lookup>? _lookups = null;
    public static List<System.Diagnostics.Process>? _processes = null;
    public static List<Service>? _services = null;

    public StatusController()
    {
        _lookups = ServiceLookup.LoadLookupFromJson("AppServices.json");
    }

    // This endpoint will return a welcome message - NO AUTHENTICATION REQUIRED
    [HttpGet("check")]
    public IActionResult CheckGeneralStatus()
    {
        // Construct the HTML response with a hyperlink
        var responseHtml = @"
        <html>
            <body>
                <p>Online.</p>
                <p><a href='check/services'>Click here to check the service status.</a></p>
            </body>
        </html>";

        // Return the HTML response with the appropriate content type
        return Content(responseHtml, "text/html");
    }

    // This endpoint will return a message indicating whether the service is running.
    // REQUIRES AUTHENTICATION
    [HttpGet("check/services")]
    [Authorize] // This attribute ensures authentication is required
    public IActionResult CheckServiceStatus()
    {
        // If we get here, the user is authenticated
        if (StatusController._lookups is null || StatusController._lookups.Count() == 0)
        {
            return BadRequest("No Lookups. Lookup File was empty or not found!");
        }

        if (StatusController._processes is null || StatusController._processes.Count() == 0)
        {
            return BadRequest("No processes. Please wait some time to lookup!");
        }

        try
        {
            // update services accordingly
            ServiceLookup.CreateOrUpdateServices(StatusController._processes, StatusController._lookups, ref StatusController._services);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
        }

        string servicesAsJson = JsonConvert.SerializeObject(_services, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto // Ensure proper serialization of interface objects
        });

        return Ok(servicesAsJson);
    }
}