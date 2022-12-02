using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.IO.Ports;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using RainApp.RainForecastJson;

namespace RainApp;

public class Worker : BackgroundService
{
    private static readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(10); 

    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var thingsApiHttpClient = new HttpClient();
        var buienradarHttpClient = new HttpClient();

        var stopwatch = new Stopwatch();

        while (!stoppingToken.IsCancellationRequested)
        {
            stopwatch.Restart();

            var things = new List<Thing>();

            var rainForecastThing = new Thing(
                "rainforecast",
                "Rain forecast"
            );
            rainForecastThing.MeasurementUnit = "%";

            things.Add(rainForecastThing);

            try
            {
                var rainResponse = await buienradarHttpClient.GetAsync("https://graphdata.buienradar.nl/2.0/forecast/geo/Rain24Hour?lat=52.11&lon=5.18");

                rainResponse.EnsureSuccessStatusCode();

                using (var responseStream = await rainResponse.Content.ReadAsStreamAsync())
                {
                    var responseParsed = await JsonSerializer.DeserializeAsync<RainForecast>(responseStream, cancellationToken: stoppingToken);
                    if (responseParsed?.forecasts != null)
                    {
                        foreach(var forecast in responseParsed.forecasts)
                        {
                            if (forecast.utcdatetime != null && forecast.value != null)
                            {
                                var datetime = DateTime.Parse(forecast.utcdatetime, null, System.Globalization.DateTimeStyles.AssumeUniversal);
                                rainForecastThing.Measurements.Add(new Measurement(datetime, forecast.value));
                            }
                        }
                    }
                }
            }
            catch(HttpRequestException apiCallError)
            {
                _logger.LogError(apiCallError, "Retrieving rain forecast failed");
            }
            catch(FormatException formattingError)
            {
                _logger.LogError(formattingError, "Rain forecast contains invalid formatted data");
            }

            stopwatch.Stop();

            _logger.LogInformation("Retrieving rain forecast took {ScanTimeSecs} seconds", stopwatch.Elapsed.TotalSeconds);

            var waitTime = _scanInterval - stopwatch.Elapsed;
            if (waitTime.Ticks < 0)
            {
                waitTime = _scanInterval;
            }
            _logger.LogInformation("Sending data to service and waiting for {WaitTimeSecs} seconds", waitTime.TotalSeconds);

            if (things.Any())
            {
                _ = PostThings(things, thingsApiHttpClient);
            }

            await Task.Delay(waitTime, stoppingToken);
        }
    }

    private async Task PostThings(IEnumerable<Thing> things, HttpClient httpClient)
    {
        var thingsJson = new StringContent(
            JsonSerializer.Serialize(things),
            Encoding.UTF8,
            Application.Json);

        using (_logger.BeginScope("Api call"))
        {
            _logger.LogInformation("API call started");
            try
            {
                var result = await httpClient.PatchAsync("http://thingify-core:80/api-rest/things", thingsJson);

                result.EnsureSuccessStatusCode();
                _logger.LogInformation("API call completed succesfully");
            }
            catch(HttpRequestException apiCallError)
            {
                _logger.LogError(apiCallError, "API call failed");
            }
        }
    }
}