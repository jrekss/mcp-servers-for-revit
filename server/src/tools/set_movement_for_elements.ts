import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMoveElementsTool(server: McpServer) {
  server.tool(
    "set_movement_for_elements",
    "Move a list of Revit elements by a specified translation vector (dX, dY, dZ) in feet.",
    {
      elementIds: z
        .array(z.number())
        .describe("Array of Revit element IDs to move"),
      dX: z
        .number()
        .describe("Translation along X axis in feet"),
      dY: z
        .number()
        .describe("Translation along Y axis in feet"),
      dZ: z
        .number()
        .describe("Translation along Z axis in feet"),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_movement_for_elements", args);
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
              text: `set_movement_for_elements failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
