using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebServer.Controllers;
using WebServer.Services;

namespace WebServer.Helper
{
    public class ServiceUpdater : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServiceUpdater> _logger;
        private TimeSpan _interval = TimeSpan.FromSeconds(30.0); // Set the interval

        /// <summary>
        /// Interval for updating services, in TimeSpan format.
        /// </summary>
        public TimeSpan Interval { get => _interval; set => _interval = value; }

        public ServiceUpdater(IServiceProvider serviceProvider, ILogger<ServiceUpdater> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Execute Async method to update services periodically.
        /// </summary>
        /// <param name="stoppingToken">token</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // load lookup from Json
                        StatusController._lookups = ServiceLookup.LoadLookupFromJson("AppServices.json");

                        // if lookups are found
                        if (StatusController._lookups is not null && StatusController._lookups.Count() != 0)
                        {
                            // find processes
                            ServiceLookup.FindProcesses(StatusController._lookups, ref StatusController._processes);

                            // avoid nulls
                            if (StatusController._processes is not null && StatusController._services is not null)
                            {
                                // update services
                                bool val = ServiceLookup.CreateOrUpdateServices(StatusController._processes, StatusController._lookups, ref StatusController._services);
                                if (val)
                                {
                                    _logger.LogInformation($"Services was updated at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"An error occurred at at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} while updating services.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }

}
