using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

/// <summary>
/// HTTP client for communicating with MCP servers using the MCP protocol
/// </summary>
public class McpHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private string? _sessionId;
    private long _messageId = 0;

    public McpHttpClient(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string url, object content)
    {
        var json = JsonSerializer.Serialize(content);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        // Only add mcp-session-id header if we have a session
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = httpContent
        };

        // MCP servers require accepting both JSON and SSE
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (!string.IsNullOrEmpty(_sessionId))
        {
            request.Headers.Add("mcp-session-id", _sessionId);
        }

        return await _httpClient.SendAsync(request);
    }

    private JsonElement ParseSseResponse(string responseText)
    {
        // SSE format: "event: message\ndata: {json}\n\n"
        // Extract the JSON from the "data:" line
        var lines = responseText.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("data: "))
            {
                var jsonData = line.Substring(6); // Remove "data: " prefix
                return JsonSerializer.Deserialize<JsonElement>(jsonData);
            }
        }

        // If no SSE format, try parsing as raw JSON
        return JsonSerializer.Deserialize<JsonElement>(responseText);
    }

    /// <summary>
    /// Initialize the MCP session
    /// </summary>
    public async Task InitializeAsync()
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref _messageId),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "weather-mcp-agent",
                    version = "1.0.0"
                }
            }
        };

        Console.WriteLine($"Sending initialize request to {_baseUrl}/mcp");
        var response = await PostJsonAsync($"{_baseUrl}/mcp", requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Initialize failed with {response.StatusCode}: {errorContent}");
        }

        // Extract session ID from response headers
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
        {
            _sessionId = sessionIds.FirstOrDefault();
        }

        // Also check lowercase header
        if (string.IsNullOrEmpty(_sessionId) && response.Headers.TryGetValues("mcp-session-id", out var sessionIds2))
        {
            _sessionId = sessionIds2.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(_sessionId))
        {
            throw new Exception("Failed to obtain session ID from MCP server");
        }

        Console.WriteLine($"MCP session initialized: {_sessionId}");

        // Send initialized notification
        var initializedNotification = new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized"
        };

        var notifyResponse = await PostJsonAsync($"{_baseUrl}/mcp", initializedNotification);
        notifyResponse.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// List available tools from the MCP server
    /// </summary>
    public async Task<List<McpTool>> ListToolsAsync()
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref _messageId),
            method = "tools/list",
            @params = new { }
        };

        var response = await PostJsonAsync($"{_baseUrl}/mcp", requestBody);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        var jsonResponse = ParseSseResponse(responseText);

        var tools = new List<McpTool>();
        if (jsonResponse.TryGetProperty("result", out var result) &&
            result.TryGetProperty("tools", out var toolsArray))
        {
            foreach (var tool in toolsArray.EnumerateArray())
            {
                var mcpTool = new McpTool
                {
                    Name = tool.GetProperty("name").GetString() ?? "",
                    Description = tool.GetProperty("description").GetString() ?? "",
                    InputSchema = tool.GetProperty("inputSchema")
                };
                tools.Add(mcpTool);
            }
        }

        return tools;
    }

    /// <summary>
    /// Call a tool on the MCP server
    /// </summary>
    public async Task<string> CallToolAsync(string toolName, Dictionary<string, object> arguments)
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref _messageId),
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = arguments
            }
        };

        var response = await PostJsonAsync($"{_baseUrl}/mcp", requestBody);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        var jsonResponse = ParseSseResponse(responseText);

        if (jsonResponse.TryGetProperty("result", out var result) &&
            result.TryGetProperty("content", out var content))
        {
            var contentArray = content.EnumerateArray().ToList();
            if (contentArray.Count > 0)
            {
                var firstContent = contentArray[0];
                if (firstContent.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "";
                }
            }
        }

        if (jsonResponse.TryGetProperty("error", out var error))
        {
            var errorMessage = error.GetProperty("message").GetString();
            throw new Exception($"MCP tool call failed: {errorMessage}");
        }

        return "";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Represents an MCP tool definition
/// </summary>
public class McpTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonElement InputSchema { get; set; }
}
