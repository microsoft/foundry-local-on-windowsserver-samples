using Microsoft.AI.Foundry.Local;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;


var alias = "qwen2.5-7b";
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


// Initialize the singleton instance.
await FoundryLocalManager.CreateAsync(config, Utils.GetAppLogger());
var mgr = FoundryLocalManager.Instance;

// Ensure that any Execution Provider (EP) downloads run and are completed.
await Utils.RunWithSpinner("Registering execution providers", mgr.EnsureEpsDownloadedAsync());

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

// Start the web service
Console.Write($"Starting web service on {config.Web.Urls}...");
await mgr.StartWebServiceAsync();
Console.WriteLine("done.");




// Step 2: Initialize MCP client and connect to weather server
Console.WriteLine("Connecting to Weather MCP Server...");
var mcpClient = new McpHttpClient("http://localhost:1000");

try
{
    await mcpClient.InitializeAsync();
    Console.WriteLine("Connected to Weather MCP Server!\n");

    // List available tools from MCP server
    var tools = await mcpClient.ListToolsAsync();
    Console.WriteLine("Available MCP Tools:");
    foreach (var tool in tools)
    {
        Console.WriteLine($"  - {tool.Name}: {tool.Description}");
    }
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"Error connecting to MCP server: {ex.Message}");
    Console.WriteLine("Make sure the weather MCP server is running on http://localhost:1000");
    Console.WriteLine("Run: cd mcp-servers/weather && npm start");
    return;
}


// Step 3: Create Semantic Kernel with OpenAI-compatible endpoint
var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(
    modelId: model.Id,
    endpoint: new Uri(foundryLocalWebUrl + "/v1"),
    apiKey: "not needed");

// Step 4: Add Weather MCP Plugin
builder.Plugins.AddFromObject(new WeatherMcpPlugin(mcpClient), "Weather");

// Step 5: Build the Kernel
var kernel = builder.Build();

// Step 6: Create chat history and get chat service
var history = new ChatHistory();
history.AddSystemMessage(@"You are a helpful weather assistant. You have access to weather tools that can:
1. Get weather alerts for US states (use two-letter state codes like CA, NY, TX)
2. Get weather forecasts for locations using latitude and longitude coordinates

When users ask about weather, use the appropriate tools to get real-time information.
Some common coordinates:
- Seattle: 47.6062, -122.3321
- New York: 40.7128, -74.0060
- Los Angeles: 34.0522, -118.2437
- Chicago: 41.8781, -87.6298
- Miami: 25.7617, -80.1918");


var chatService = kernel.GetRequiredService<IChatCompletionService>();


// Step 7: Interactive chat loop
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

    history.AddUserMessage(prompt);
    StringBuilder fullResponse = new StringBuilder();

    try
    {
        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.1
        };

        // Stream the response with automatic tool invocation
        await foreach (var response in chatService.GetStreamingChatMessageContentsAsync(
            history,
            executionSettings: executionSettings,
            kernel: kernel
        ))
        {
            fullResponse.Append(response.Content);
            Console.Write(response.Content);
        }

        Console.WriteLine("\n");
        history.AddAssistantMessage(fullResponse.ToString());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
}


// Tidy up
// Stop the web service and unload model
Console.WriteLine("Goodbye!");
mcpClient.Dispose();
await mgr.StopWebServiceAsync();
await model.UnloadAsync();
