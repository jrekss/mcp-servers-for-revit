import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSetParameterValueTool(server: McpServer) {
  server.tool(
    "set_parameter_value_for_elements",
    "Set the value of a specific parameter for a list of Revit element IDs. Supports String, Integer, Double/Float, and ElementId parameter types.",
    {
      elementIds: z
        .array(z.number())
        .describe("Array of Revit element IDs to update"),
      parameterNameOrId: z
        .string()
        .describe("The name of the parameter or its built-in parameter integer ID"),
      value: z
        .string()
        .describe("The value to set (e.g. 'New Name', '120', '4.5')"),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_parameter_value_for_elements", args);
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
              text: `set_parameter_value_for_elements failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
