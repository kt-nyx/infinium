import type { Settings } from "../../shared/types";
import { logger } from "../logging";
import {
  NexusClient,
  NexusConfigError,
  NexusError,
  NexusUnavailableError,
} from "./nexusClient";

export interface NexusHealthStatus {
  ok: boolean;
  message: string;
}

export const checkNexusHealth = async (settings: Settings): Promise<NexusHealthStatus> => {
  if (!settings.nexusApiKey || !settings.nexusApiKey.trim()) {
    return {
      ok: false,
      message:
        "No Nexus personal API key is configured. Add your key in Settings to enable Nexus-powered analysis.",
    };
  }

  const client = new NexusClient(
    {
      ...settings,
      // Ensure a default game hint; the health check itself does not depend on
      // the game, but NexusClient expects a game identifier.
      analysisDefaults: settings.analysisDefaults,
    },
    "SkyrimSE",
  );

  try {
    await client.checkHealth();
    return {
      ok: true,
      message: "Successfully connected to the Nexus Mods GraphQL API with the configured key.",
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);

    if (error instanceof NexusConfigError) {
      await logger.warn(`[NexusHealth] Configuration error during health check: ${message}`);
      return {
        ok: false,
        message:
          "The configured Nexus API key was rejected or is invalid. Double-check the key on your Nexus account page and paste it again.",
      };
    }

    if (error instanceof NexusUnavailableError) {
      await logger.warn(`[NexusHealth] Nexus API appears temporarily unavailable: ${message}`);
      return {
        ok: false,
        message:
          "The Nexus Mods API is currently unavailable or rate-limited. Please wait a bit and try again.",
      };
    }

    if (error instanceof NexusError) {
      await logger.warn(`[NexusHealth] Nexus error during health check: ${message}`);
      return {
        ok: false,
        message: `Encountered an error while talking to the Nexus API: ${message}`,
      };
    }

    await logger.warn(`[NexusHealth] Unexpected error during health check: ${message}`);
    return {
      ok: false,
      message: "Unexpected error while checking Nexus connectivity. See logs for details.",
    };
  }
};




