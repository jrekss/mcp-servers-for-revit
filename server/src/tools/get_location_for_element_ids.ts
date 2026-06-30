import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetLocationTool(server: McpServer) {
  server.tool(
    "get_location_for_element_ids",
    "Get location point coordinates (XYZ in feet) or curve endpoints (start/end XYZ) for a list of Revit element IDs.",
    {
      elementIds: z
        .array(z.number())
        .describe("Array of Revit element IDs to retrieve locations for"),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_location_for_element_ids", args);
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
              text: `get_location_for_element_ids failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
