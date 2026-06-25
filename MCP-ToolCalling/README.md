# Foundry Local MCP Integration

An example demonstrating how to integrate MCP servers with Foundry Local using tool calling. A custom Weather MCP server exposes real-time weather data, and a .NET agent uses **Microsoft.Extensions.AI** over Foundry Local's OpenAI-compatible endpoint to connect the locally-running model to those tools. The MCP tools auto-wire as `AIFunction`s and are invoked automatically — no Semantic Kernel and no manual tool loop required.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│              foundry-local-mcp-agent (.NET)          │
│                                                      │
│  ┌─────────────────┐      ┌────────────────────────┐ │
│  │  Foundry Local  │      │  Microsoft.Extensions.AI│ │
│  │  (qwen2.5-7b)   │◄────►│  function-invoking      │ │
│  │  port 9001      │      │  IChatClient + MCP tools│ │
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
> Then update the MCP endpoint URI in [Program.cs](foundry-local-mcp-agent/Program.cs) to match — see the `HttpClientTransport` `Endpoint` (`http://localhost:3000/mcp`).

## Step 2: Run the Foundry Local MCP Agent

The agent is a .NET 9 console application that:

1. Starts an embedded **Foundry Local** instance, loads the `qwen2.5-7b` model, and starts its OpenAI-compatible web service
2. Connects to the Weather MCP Server using the `ModelContextProtocol` HTTP transport
3. Builds a function-invoking `IChatClient` (Microsoft.Extensions.AI) over the Foundry Local endpoint and passes the MCP tools (which are `AIFunction`s) directly to it
4. Runs an interactive chat loop — `UseFunctionInvocation()` automatically executes any requested MCP tools and feeds the results back to the model

Open a second terminal and run:

```bash
cd foundry-local-mcp-agent
dotnet run                 # defaults to qwen2.5-7b
dotnet run -- qwen2.5-7b   # or pass a model alias explicitly
```

On first run, the model will be downloaded. Subsequent runs reuse the cached model.

## Usage

Once running, type natural language weather questions at the `>` prompt. The model decides which MCP tool to call; `Microsoft.Extensions.AI` executes it via the MCP client automatically and returns the result to the model for a final, grounded answer.

**Example queries:**

```
> What are the weather alerts in California?
> What's the forecast for Seattle?
> Are there any warnings in Texas right now?
> Give me the forecast for New York City.
```

Type `exit` to quit. The agent disconnects from the MCP server, stops the web service, and unloads the model on exit.

## Project Structure

```
MCP-ToolCalling/
├── mcp-servers/
│   └── weather/
│       ├── weather.ts          # MCP server implementation (Express + MCP SDK)
│       ├── package.json
│       └── tsconfig.json
└── foundry-local-mcp-agent/
    ├── Program.cs              # Foundry Local setup, MCP client, function-invoking chat loop
    └── foundry-local-mcp-agent.csproj
```

## Key Dependencies

| Component | Package |
|---|---|
| Local model hosting | `Microsoft.AI.Foundry.Local.WinML` |
| Chat client + automatic tool invocation | `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, `OpenAI` |
| MCP client + HTTP transport | `ModelContextProtocol` |
| Weather data | US National Weather Service API (no API key required) |

## Troubleshooting

**`Error connecting to MCP server`** — Make sure the Weather MCP Server is running before starting the agent. Run `npm run start` from `mcp-servers/weather` first.

**Tools are never called / the model prints `<tool_call>` as text** — Use a non-thinking **instruct** model (e.g. `qwen2.5-7b`). Foundry Local's server-side parser cannot extract tool calls from reasoning models like Qwen3 that emit a `<think>` block before the call. The agent uses non-streaming completions, which is required because Foundry Local only populates structured `tool_calls` on non-streaming responses.

**`Failed to allocate memory` / ONNX BFC arena error on multi-GPU machines** — The CUDA execution provider may bind to a GPU too small to hold the model. CUDA targets GPU `0` by default when `CUDA_VISIBLE_DEVICES` is unset; if that GPU is too small, pin the process to a larger one by setting the variable yourself before running, e.g. `CUDA_VISIBLE_DEVICES=1 dotnet run`.

**Weather data only covers the US** — The NWS API only covers US locations. State alert codes must be two-letter US state abbreviations (e.g. `CA`, `NY`, `TX`). Forecast coordinates outside the US will return an error from the NWS API.
