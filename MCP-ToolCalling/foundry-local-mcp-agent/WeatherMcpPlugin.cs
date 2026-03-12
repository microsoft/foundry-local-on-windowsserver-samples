using System.ComponentModel;
using Microsoft.SemanticKernel;

/// <summary>
/// Semantic Kernel plugin that exposes weather MCP tools
/// </summary>
public class WeatherMcpPlugin
{
    private readonly McpHttpClient _mcpClient;

    public WeatherMcpPlugin(McpHttpClient mcpClient)
    {
        _mcpClient = mcpClient;
    }

    /// <summary>
    /// Get weather alerts for a US state
    /// </summary>
    /// <param name="state">Two-letter US state code (e.g. CA, NY)</param>
    /// <returns>Weather alerts for the specified state</returns>
    [KernelFunction("get_weather_alerts")]
    [Description("Get weather alerts for a US state. Use two-letter state codes like CA, NY, TX, etc.")]
    public async Task<string> GetWeatherAlertsAsync(
        [Description("Two-letter US state code (e.g. CA, NY, TX)")] string state)
    {
        try
        {
            var arguments = new Dictionary<string, object>
            {
                { "state", state.ToUpper() }
            };

            var result = await _mcpClient.CallToolAsync("get_alerts", arguments);
            return result;
        }
        catch (Exception ex)
        {
            return $"Error getting weather alerts: {ex.Message}";
        }
    }

    /// <summary>
    /// Get weather forecast for a location
    /// </summary>
    /// <param name="latitude">Latitude of the location</param>
    /// <param name="longitude">Longitude of the location</param>
    /// <returns>Weather forecast for the specified location</returns>
    [KernelFunction("get_weather_forecast")]
    [Description("Get weather forecast for a geographic location using latitude and longitude coordinates. For example, Seattle is at latitude 47.6062, longitude -122.3321.")]
    public async Task<string> GetWeatherForecastAsync(
        [Description("Latitude of the location (e.g. 47.6062 for Seattle)")] double latitude,
        [Description("Longitude of the location (e.g. -122.3321 for Seattle)")] double longitude)
    {
        try
        {
            var arguments = new Dictionary<string, object>
            {
                { "latitude", latitude },
                { "longitude", longitude }
            };

            var result = await _mcpClient.CallToolAsync("get_forecast", arguments);
            return result;
        }
        catch (Exception ex)
        {
            return $"Error getting weather forecast: {ex.Message}";
        }
    }
}
