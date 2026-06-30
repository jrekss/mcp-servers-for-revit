import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementsByCategoryTool(server: McpServer) {
  server.tool(
    "get_elements_by_category",
    "Get all elements matching category names (e.g. 'Walls', 'Doors') or built-in category integer IDs (e.g. '-2000011' for OST_Walls) across the entire project.",
    {
      categoryNamesOrIds: z
        .array(z.string())
        .describe("Array of category names or BuiltInCategory integer IDs as strings to look up"),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_elements_by_category", args);
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
              text: `get_elements_by_category failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
