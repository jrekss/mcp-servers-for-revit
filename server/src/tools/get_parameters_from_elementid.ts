import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetParametersTool(server: McpServer) {
  server.tool(
    "get_parameters_from_elementid",
    "Get all parameters (Name, Value, StorageType, IsReadOnly, Group) for a specific Revit element ID.",
    {
      elementId: z
        .number()
        .describe("The Revit element ID to retrieve parameters for"),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_parameters_from_elementid", args);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `get_parameters_from_elementid failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
