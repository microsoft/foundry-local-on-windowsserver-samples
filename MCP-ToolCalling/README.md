# Foundry Local MCP Integration

An example demonstrating how to integrate MCP servers with Foundry Local and Semantic Kernel using tool calling. A custom Weather MCP server exposes real-time weather data, and a .NET agent uses Semantic Kernel to connect Foundry Local's locally-running model to those tools.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│              foundry-local-mcp-agent (.NET)          │
│                                                      │
│  ┌─────────────────┐      ┌────────────────────────┐ │
│  │  Foundry Local  │      │   Semantic Kernel       │ │
│  │  (qwen2.5-7b)   │◄────►│   + WeatherMcpPlugin   │ │
│  │  port 9001      │      │                        │ │
│  └─────────────────┘      └──────────┬─────────────┘ │
└─────────────────────────────────────┼───────────────┘
                                       │ MCP over HTTP
                          ┌────────────▼───────────┐
                          │  Weather MCP Server     │
                          │  (Node.js / TypeScript) │
                          │  port 3000              │
                          │                         │
                          │  Tools:                 │
                          │  • get_alerts           │
                          │  • get_forecast         │
                          └────────────────────────┘
                                       │
                          ┌────────────▼───────────┐
                          │  National Weather       │
                          │  Service API (NWS)      │
                          └────────────────────────┘
```

## Prerequisites

- [Node.js](https://nodejs.org/) v18 or later
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)

## Step 1: Start the Weather MCP Server

The Weather MCP Server is a Node.js/TypeScript server that exposes two tools via the MCP Streamable HTTP transport:

- **`get_alerts`** — fetches active weather alerts for a US state from the National Weather Service API
- **`get_forecast`** — fetches a multi-day weather forecast for any lat/lon coordinates

```bash
cd mcp-servers/weather
npm install
npm run build
npm run start
```

The server starts on `http://localhost:3000`. You can verify it is running by navigating to `http://localhost:3000/` which returns server metadata.

> To use a different port, set the `PORT` environment variable before starting:
> ```bash
> PORT=4000 npm run start
> ```
> Then update the `McpHttpClient` initialization in [Program.cs](foundry-local-mcp-agent/Program.cs) to match.

## Step 2: Run the Foundry Local MCP Agent

The agent is a .NET 9 console application that:

1. Starts an embedded **Foundry Local** instance and downloads/loads the `qwen2.5-7b` model
2. Connects to the Weather MCP Server via `McpHttpClient`
3. Registers a **Semantic Kernel** plugin (`WeatherMcpPlugin`) that wraps the MCP tools
4. Runs an interactive chat loop where the model automatically invokes weather tools as needed

Open a second terminal and run:

```bash
cd foundry-local-mcp-agent
dotnet run
```

On first run, the model will be downloaded. Subsequent runs reuse the cached model.

## Usage

Once running, type natural language weather questions at the `>` prompt. The model uses Semantic Kernel's automatic tool invocation (`FunctionChoiceBehavior.Auto()`) to decide which MCP tool to call.

**Example queries:**

```
> What are the weather alerts in California?
> What's the forecast for Seattle?
> Are there any warnings in Texas right now?
> Give me the forecast for New York City.
```

Type `exit` to quit. The agent will stop the Foundry Local web service and unload the model on exit.

## Project Structure

```
MCP-ToolCalling/
├── mcp-servers/
│   └── weather/
│       ├── weather.ts          # MCP server implementation (Express + MCP SDK)
│       ├── package.json
│       └── tsconfig.json
└── foundry-local-mcp-agent/
    ├── Program.cs              # Entry point: Foundry Local setup + chat loop
    ├── McpHttpClient.cs        # MCP Streamable HTTP client (initialize/list tools/call tool)
    ├── WeatherMcpPlugin.cs     # Semantic Kernel plugin wrapping MCP tools
    ├── Utils.cs                # Helper utilities (spinner, logging)
    └── foundry-local-mcp-agent.csproj
```

## Key Dependencies

| Component | Package |
|---|---|
| Local model hosting | `Microsoft.AI.Foundry.Local.WinML` |
| AI orchestration | `Microsoft.SemanticKernel` |
| OpenAI-compatible client | `Microsoft.SemanticKernel.Connectors.OpenAI` |
| MCP transport | Custom `McpHttpClient` over HTTP |
| Weather data | US National Weather Service API (no API key required) |

## Troubleshooting

**`Error connecting to MCP server`** — Make sure the Weather MCP Server is running before starting the agent. Run `npm run start` from `mcp-servers/weather` first.

**Weather data only covers the US** — The NWS API only covers US locations. State alert codes must be two-letter US state abbreviations (e.g. `CA`, `NY`, `TX`). Forecast coordinates outside the US will return an error from the NWS API.
