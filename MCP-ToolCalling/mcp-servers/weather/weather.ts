import express from "express";
import cors from "cors";
import { randomUUID } from "node:crypto";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { isInitializeRequest } from "@modelcontextprotocol/sdk/types.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

// Constants for NWS API
const NWS_API_BASE = "https://api.weather.gov";
const USER_AGENT = "weather-mcp-server/1.0";

// Helper function to make NWS API requests
async function makeNWSRequest(url: string): Promise<any | null> {
  try {
    const response = await fetch(url, {
      headers: {
        "User-Agent": USER_AGENT,
        "Accept": "application/geo+json"
      },
      signal: AbortSignal.timeout(30000) // 30 second timeout
    });
    
    if (!response.ok) {
      console.error(`NWS API request failed: ${response.status} ${response.statusText}`);
      return null;
    }
    
    return await response.json();
  } catch (error) {
    console.error(`NWS API request error:`, error);
    return null;
  }
}

// Helper function to format alert features
function formatAlert(feature: any): string {
  const props = feature.properties;
  return `
Event: ${props.event || 'Unknown'}
Area: ${props.areaDesc || 'Unknown'}
Severity: ${props.severity || 'Unknown'}
Description: ${props.description || 'No description available'}
Instructions: ${props.instruction || 'No specific instructions provided'}
`;
}

// Weather tool implementations
async function getAlertsForState(state: string): Promise<string> {
  console.log(`Getting alert for ${state}`);
  const url = `${NWS_API_BASE}/alerts/active/area/${state}`;
  const data = await makeNWSRequest(url);

  if (!data || !data.features) {
    return "Unable to fetch alerts or no alerts found.";
  }

  if (data.features.length === 0) {
    return "No active alerts for this state.";
  }

  const alerts = data.features.map(formatAlert);
  return alerts.join("\n---\n");
}

async function getForecastForLocation(latitude: number, longitude: number): Promise<string> {
  console.log(`Getting forecast for location: ${latitude}, ${longitude}`);
  // First get the forecast grid endpoint
  const pointsUrl = `${NWS_API_BASE}/points/${latitude},${longitude}`;
  const pointsData = await makeNWSRequest(pointsUrl);

  if (!pointsData) {
    return "Unable to fetch forecast data for this location.";
  }

  // Get the forecast URL from the points response
  const forecastUrl = pointsData.properties.forecast;
  const forecastData = await makeNWSRequest(forecastUrl);

  if (!forecastData) {
    return "Unable to fetch detailed forecast.";
  }

  // Format the periods into a readable forecast
  const periods = forecastData.properties.periods;
  const forecasts = periods.slice(0, 5).map((period: any) => {
    return `
${period.name}:
Temperature: ${period.temperature}°${period.temperatureUnit}
Wind: ${period.windSpeed} ${period.windDirection}
Forecast: ${period.detailedForecast}
`;
  });

  return forecasts.join("\n---\n");
}

// Create and configure the MCP server
function getServer() {
  const server = new Server({
    name: "weather",
    version: "1.0.0",
  }, {
    capabilities: {
      tools: {},
    },
  });

  server.setRequestHandler(ListToolsRequestSchema, async () => {
    return {
      tools: [
        {
          name: "get_alerts",
          description: "Get weather alerts for a US state",
          inputSchema: {
            type: "object",
            properties: {
              state: {
                type: "string",
                description: "Two-letter US state code (e.g. CA, NY)"
              }
            },
            required: ["state"]
          }
        },
        {
          name: "get_forecast",
          description: "Get weather forecast for a location",
          inputSchema: {
            type: "object",
            properties: {
              latitude: {
                type: "number",
                description: "Latitude of the location"
              },
              longitude: {
                type: "number",
                description: "Longitude of the location"
              }
            },
            required: ["latitude", "longitude"]
          }
        }
      ]
    };
  });

  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;
    
    if (!args) {
      throw new Error(`No arguments provided for tool: ${name}`);
    }

    switch (name) {
      case "get_alerts":
        const alertsResult = await getAlertsForState(args.state as string);
        return { 
          content: [{ 
            type: "text", 
            text: alertsResult 
          }] 
        };
        
      case "get_forecast":
        const forecastResult = await getForecastForLocation(
          args.latitude as number, 
          args.longitude as number
        );
        return { 
          content: [{ 
            type: "text", 
            text: forecastResult 
          }] 
        };
        
      default:
        throw new Error(`Unknown tool: ${name}`);
    }
  });

  return server;
}

// Create Express app
const app = express();
app.use(express.json());
app.use(cors({
  origin: '*',
  exposedHeaders: ['Mcp-Session-Id'],
  allowedHeaders: ['Content-Type', 'mcp-session-id'],
}));

// Map to store transports by session ID
const transports: { [sessionId: string]: StreamableHTTPServerTransport } = {};

// POST handler for client-to-server communication
app.post('/mcp', async (req, res) => {
  const sessionId = req.headers['mcp-session-id'] as string | undefined;
  let transport: StreamableHTTPServerTransport;

  if (sessionId && transports[sessionId]) {
    transport = transports[sessionId];
  } else if (!sessionId && isInitializeRequest(req.body)) {
    transport = new StreamableHTTPServerTransport({
      sessionIdGenerator: () => randomUUID(),
      onsessioninitialized: (newSessionId) => {
        transports[newSessionId] = transport;
      },
    });
    transport.onclose = () => {
      if (transport.sessionId) {
        delete transports[transport.sessionId];
      }
    };
    const server = getServer();
    await server.connect(transport);
  } else {
    res.status(400).json({
      jsonrpc: '2.0',
      error: {
        code: -32000,
        message: 'Bad Request: No valid session ID provided',
      },
      id: null,
    });
    return;
  }
  await transport.handleRequest(req, res, req.body);
});

// Reusable handler for GET and DELETE requests
const handleSessionRequest = async (req: express.Request, res: express.Response) => {
  const sessionId = req.headers['mcp-session-id'] as string | undefined;
  if (!sessionId || !transports[sessionId]) {
    res.status(400).send('Invalid or missing session ID');
    return;
  }
  const transport = transports[sessionId];
  await transport.handleRequest(req, res);
};

// GET for server-to-client notifications via SSE
app.get('/mcp', handleSessionRequest);

// DELETE for session termination
app.delete('/mcp', handleSessionRequest);

// Root endpoint for server info
app.get('/', (req, res) => {
  res.json({
    name: "weather",
    version: "1.0.0",
    description: "Weather MCP Server",
    tools: ["get_alerts", "get_forecast"],
    endpoints: {
      mcp: "/mcp"
    }
  });
});

// Start the server
const PORT = process.env.PORT ? Number(process.env.PORT) : 1000;
app.listen(PORT, (error?: any) => {
  if (error) {
    console.error('Failed to start server:', error);
    process.exit(1);
  }
  console.log(`Weather MCP Server listening on port ${PORT}`);
  console.log(`Server info available at: http://localhost:${PORT}/`);
});