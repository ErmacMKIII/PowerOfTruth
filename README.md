# Power of Truth

Power of Truth Service Dashboard.
Displays information about services - running processes or services on some PC.

# How to Use

Use appsettings.json to setup local IP and Http/Https ports for both WebServer and WebClient.

Power of Truth Dashboard administrator needs to fill Json input file of lookup.
Default name is: "AppServices.json" and cannot be changed.

Located in `WebServer` directory.

App consists of lookup Json Input File which looks like this:
```
[
  {
    "Name": "Demolition Synergy",
    "Description": "A game server process for Demolition Synergy",
    "AppIcon": "dsynergy-server.png",
    "ProcessName": "java",
    "ProcessId": null
  },
  {
    "Name": "Teamspeak 3 Server",
    "Description": "Gaming VOIP Server",
    "AppIcon": "teamspeak3-server.png",
    "ProcessName": "ts3server",
    "ProcessId": null
  }
]
```
Located in `wwwroot/assets` of `WebClient`,
supported app icons are:

| FileName                           | Image                                                     |
| ----------------------------------- | --------------------------------------------------------- |
| dont-starve-together-server.png     | ![dont-starve-together-server](WebClient/wwwroot/assets/dont-starve-together-server.png) |
| dsynergy-server.png                 | ![dsynergy-server](WebClient/wwwroot/assets/dsynergy-server.png)     |
| minecraft-server.png                | ![minecraft-server](WebClient/wwwroot/assets/minecraft-server.png)   |
| starbound-server.png                | ![starbound-server](WebClient/wwwroot/assets/starbound-server.png)   |
| teamspeak3-musicbot.png             | ![teamspeak3-musicbot](WebClient/wwwroot/assets/teamspeak3-musicbot.png) |
| teamspeak3-server.png               | ![teamspeak3-server](WebClient/wwwroot/assets/teamspeak3-server.png) |

In case supplied `AppIcon` in lookup Json is null or not any of `FileName` values in table, default one is used. 
![Default](WebClient/wwwroot/assets/default.png)

Either process name or ProcessId must be supplied.
For each item of Json array.

Hidden WebAPI (requires credentials to be used) is called.
![Alt text](./Misc/WebServer.png?raw=true "WebServer")

Go and launch 
Power of Truth Dashboard Machine IP in the browser.
Table with actual services will be returned.

![Alt text](./Misc/WebClient.png?raw=true "WebClient")

# Solution


Power of Truth App solution consists of two projects as a working together bundle.
One of them `WebServer` having ASP.NET Web API as back-end and doing lookup of processes (services) running on the host machine (where is deployed).
Second is `WebClient` being ASP.NET Web App (front-end) for the former, calling Web API and displays info and status of all services. Also on host machine. 

# Rule about Source Control

Keep git commits messages concise and sensible.
Do not commit direcly into `master` branch.

Branch `master` always contains tested and stable code. It is meant for production.
Branch `dev` is meant for development.

Once development is tested and proven okay make pull requests from `dev` and merge them into `master`.

Make releases from `master` branch.

This rule keeps good practice.

# How To Build

Visual Studio 2022 is needed.

**Project is property of 
Power of Truth Dashboard. Need explicit permission to view or work on it.**

Clone the project via Visual Studio 2022.

Solution consists of two projects which needs to be deployed to the remote machine.

Use Visual Studio 2022 GUI Clean & Rebuild of the solution.
Which should be straight-forward.

# How To Deploy

They need to be deployed on the remote machine of 
Power of Truth Dashboard. Both.

Please use Developer Shell of Visual Studio 2022.

To publish WebServer please use following commands:
```
cd C:\Users\coas9\source\repos\
PowerOfTruthApp\WebServer
```
```
dotnet publish -c Release -o ../publish/WebServer
```

Similarly, to publish WebClient please use following commands:
```
cd C:\Users\coas9\source\repos\
PowerOfTruthApp\WebClient
```
```
dotnet publish -c Release -o ../publish/WebClient
```

Compress `WebServer` directory to ZIP file and so `WebClient` directory to the ZIP file.

DotNet 6.0 Runtime (or SDK) must be installed on 
Power of Truth Dashboard machine where is deployed.
Could be checked with, in Command Prompt or PowerShell:
```
dotnet --version
```

Extract ZIP archives (for instance, to Desktop). On remote 
Power of Truth Dashboard machine.

With Command Prompt or Windows PowerShell run the DLLs in following way:
```
cd Desktop\WebServer
```
```
set ASPNETCORE_URLS=http://localhost:5000;https://localhost:44343
```
```
dotnet WebServer.dll
```
and in separate instance of Command Prompt or Windows PowerShell run:
```
cd Desktop\WebClient
```
```
set ASPNETCORE_URLS=http://localhost:5001;https://localhost:44300
```
```
dotnet WebClient.dll
```

WebServer instance and WebClient instance is now running.

Done.

# Automated Deployment from Release

Download WebServer.zip and WebClient.zip binaries from the release.
Download [AutomateDeploy.cmd](Utils/AutomateDeploy.cmd) from here or from release.

Run the script file.

Verify it works okay and instances are running.

Done.

# Firewall & Port forwarding

Make sure that TCP following ports below are allowed by Windows Firewall.
And Port forwarded is being configured on router for TCP protocol.

WebServer:

Internal Port: 44343 (HTTPS) or 5000 (HTTP)
External Port: You can keep it the same as the internal port or choose a different one (e.g., 44343).

WebClient:

Internal Port: 44300 (HTTPS) or 5001 (HTTP)
External Port: You can keep it the same as the internal port or choose a different one (e.g., 44300).

Now the endpoints can be accessed publicly from the Internet.