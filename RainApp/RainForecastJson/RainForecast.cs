using System.Collections.Generic;

namespace RainApp.RainForecastJson;

class RainForecast
{
    public decimal? lat { get; set; }

    public decimal? lon { get; set; }

    public List<RainForecastTimeframe>? forecasts { get; set; }
}