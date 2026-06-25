using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAI;
using System.ClientModel;


var alias = args.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))?.Trim() ?? "qwen2.5-7b";
var ct = CancellationToken.None;
var foundryLocalWebUrl = "http://127.0.0.1:9001";


// Step 1: Start Foundry Local instance
var config = new Configuration
{
    AppName = "foundry-local-mcp-agent",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
    Web = new Configuration.WebService
    {
        Urls = foundryLocalWebUrl
    }
};

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
});

// Initialize the singleton instance.
await FoundryLocalManager.CreateAsync(config, loggerFactory.CreateLogger("foundry-local-mcp-agent"));
var mgr = FoundryLocalManager.Instance;

// Discover available execution providers and their registration status.
var eps = mgr.DiscoverEps();
int maxNameLen = 30;
Console.WriteLine("Available execution providers:");
Console.WriteLine($"  {"Name".PadRight(maxNameLen)}  Registered");
Console.WriteLine($"  {new string('─', maxNameLen)}  {"──────────"}");
foreach (var ep in eps)
{
    Console.WriteLine($"  {ep.Name.PadRight(maxNameLen)}  {ep.IsRegistered}");
}

// Download and register all execution providers with per-EP progress.
// EP packages include dependencies and may be large.
// Download is only required again if a new version of the EP is released.
// For cross platform builds there is no dynamic EP download and this will return immediately.
Console.WriteLine("\nDownloading execution providers:");
if (eps.Length > 0)
{
    var currentEp = "";
    await mgr.DownloadAndRegisterEpsAsync((epName, percent) =>
    {
        if (epName != currentEp)
        {
            if (currentEp != "") Console.WriteLine();
            currentEp = epName;
        }
        Console.Write($"\r  {epName.PadRight(maxNameLen)}  {percent,6:F1}%");
    });
    if (currentEp != "") Console.WriteLine();
}
else
{
    Console.WriteLine("No execution providers to download.");
}

// Get the model catalog
var catalog = await mgr.GetCatalogAsync();

// Get a model using an alias
var model = await catalog.GetModelAsync(alias) ?? throw new Exception("Model not found");
// Download the model (the method skips download if already cached)
await model.DownloadAsync(progress =>
{
    Console.Write($"\rDownloading model: {progress:F2}%");
    if (progress >= 100f)
    {
        Console.WriteLine();
    }
});

// Load the model
Console.Write($"Loading model {model.Id}...");
await model.LoadAsync();
Console.WriteLine("done.");

// Start the OpenAI-compatible web service
Console.Write($"Starting web service on {foundryLocalWebUrl}...");
await mgr.StartWebServiceAsync();
Console.WriteLine("done.");


// Step 2: Initialize MCP client and connect to weather server
Console.WriteLine("Connecting to Weather MCP Server...");
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "WeatherMCP",
    Endpoint = new Uri("http://localhost:3000/mcp"),
});

McpClient mcpClient = await McpClient.CreateAsync(transport);
Console.WriteLine("Connected to Weather MCP Server!\n");

// List available tools from MCP server
var tools = await mcpClient.ListToolsAsync();
Console.WriteLine("Available MCP Tools:");
foreach (var tool in tools)
{
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
}
Console.WriteLine();


// Step 3: Build a function-invoking chat client over Foundry Local's OpenAI-compatible endpoint.
// MCP tools are already AIFunctions, so they plug in directly and are invoked automatically by UseFunctionInvocation().
// Non-streaming GetResponseAsync is used because Foundry Local only populates structured tool calls on non-streaming responses.
var openAIClient = new OpenAIClient(
    new ApiKeyCredential("not-needed"),
    new OpenAIClientOptions { Endpoint = new Uri($"{foundryLocalWebUrl}/v1") });

IChatClient chatClient = openAIClient
    .GetChatClient(model.Id)
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var options = new ChatOptions
{
    Tools = [.. tools],
    Temperature = 0.1f
};

var messages = new List<ChatMessage>
{
    new(ChatRole.System, @"You are a helpful weather assistant. You have access to weather tools that can:
1. Get weather alerts for US states (use two-letter state codes like CA, NY, TX)
2. Get weather forecasts for locations using latitude and longitude coordinates

When users ask about weather, use the appropriate tools to get real-time information.
Some common coordinates:
- Seattle: 47.6062, -122.3321
- New York: 40.7128, -74.0060
- Los Angeles: 34.0522, -118.2437
- Chicago: 41.8781, -87.6298
- Miami: 25.7617, -80.1918")
};


// Step 4: Interactive chat loop
Console.WriteLine("Chat with the Weather Assistant (type 'exit' to quit)");
Console.WriteLine("Try asking questions like:");
Console.WriteLine("  - What are the weather alerts in California?");
Console.WriteLine("  - What's the forecast for Seattle?");
Console.WriteLine("  - Are there any weather warnings in Texas?");
Console.WriteLine();

var prompt = string.Empty;

while (prompt.CompareTo("exit") != 0)
{
    Console.Write("> ");
    prompt = Console.ReadLine() ?? string.Empty;

    if (prompt.CompareTo("exit") == 0)
        break;

    if (string.IsNullOrWhiteSpace(prompt))
        continue;

    messages.Add(new(ChatRole.User, prompt));

    try
    {
        // The function-invoking client handles the full tool loop: it calls the model,
        // executes any requested MCP tools, feeds the results back, and returns the
        // final grounded answer.
        var response = await chatClient.GetResponseAsync(messages, options, ct);

        // Print only the final assistant message. response.Text concatenates every
        // assistant turn, which would also include the intermediate tool-calling turn
        // (some models, e.g. Qwen, echo the <tool_call> JSON as content there).
        var answer = response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text;
        Console.WriteLine((string.IsNullOrWhiteSpace(answer) ? response.Text : answer) + "\n");
        messages.AddMessages(response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
}


// Tidy up - disconnect MCP, stop the web service, and unload the model
Console.WriteLine("Goodbye!");
await mcpClient.DisposeAsync();
await mgr.StopWebServiceAsync();
await model.UnloadAsync();
