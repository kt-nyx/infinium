import { describe, expect, it, vi } from "vitest";
import type { Settings } from "../src/shared/types";
import { checkNexusHealth } from "../src/main/nexus/nexusHealth";
import {
  NexusClient,
  NexusConfigError,
  NexusError,
  NexusUnavailableError,
} from "../src/main/nexus/nexusClient";

vi.mock("../src/main/nexus/nexusClient");

const baseSettings: Settings = {
  mo2RootGuess: undefined,
  mo2Instances: [],
  selectedInstanceId: undefined,
  selectedProfileId: undefined,
  skyrimSeDataPath: undefined,
  lootPortablePath: undefined,
  lootInstalledPath: undefined,
  lootMode: "auto",
  lootCustomPath: undefined,
  nexusApiKey: "test-key",
  ragIndexPath: undefined,
  analysisMode: "offline",
  analysisDefaults: {
    useLoot: true,
    useNexus: true,
    useRag: false,
    complexity: 3,
    opinionatedness: 3,
  },
  logLevel: "info",
};

describe("checkNexusHealth", () => {
  it("reports missing API key when none is configured", async () => {
    const settings: Settings = { ...baseSettings, nexusApiKey: "" };
    const result = await checkNexusHealth(settings);
    expect(result.ok).toBe(false);
    expect(result.message.toLowerCase()).toContain("no nexus personal api key");
  });

  it("returns ok when NexusClient.checkHealth succeeds", async () => {
    const settings: Settings = { ...baseSettings };

    const MockClient = NexusClient as unknown as vi.Mock;
    const instance = { checkHealth: vi.fn().mockResolvedValue(undefined) };
    MockClient.mockImplementation(function MockedClient() {
      return instance;
    });

    const result = await checkNexusHealth(settings);
    expect(instance.checkHealth).toHaveBeenCalledTimes(1);
    expect(result.ok).toBe(true);
    expect(result.message.toLowerCase()).toContain("successfully connected");
  });

  it("maps NexusConfigError to a clear invalid-key message", async () => {
    const settings: Settings = { ...baseSettings };

    const MockClient = NexusClient as unknown as vi.Mock;
    const instance = {
      checkHealth: vi.fn().mockRejectedValue(
        new NexusConfigError("API key rejected with status 401"),
      ),
    };
    MockClient.mockImplementation(function MockedClient() {
      return instance;
    });

    const result = await checkNexusHealth(settings);
    expect(result.ok).toBe(false);
    expect(result.message.toLowerCase()).toContain("key was rejected");
  });

  it("maps NexusUnavailableError to a temporary-issue message", async () => {
    const settings: Settings = { ...baseSettings };

    const MockClient = NexusClient as unknown as vi.Mock;
    const instance = {
      checkHealth: vi.fn().mockRejectedValue(
        new NexusUnavailableError("rate limited by upstream"),
      ),
    };
    MockClient.mockImplementation(function MockedClient() {
      return instance;
    });

    const result = await checkNexusHealth(settings);
    expect(result.ok).toBe(false);
    expect(result.message.toLowerCase()).toContain("rate-limited");
  });

  it("maps generic NexusError to a generic Nexus error message", async () => {
    const settings: Settings = { ...baseSettings };

    const MockClient = NexusClient as unknown as vi.Mock;
    const instance = {
      checkHealth: vi.fn().mockRejectedValue(
        new NexusError("some unexpected nexus error occurred"),
      ),
    };
    MockClient.mockImplementation(function MockedClient() {
      return instance;
    });

    const result = await checkNexusHealth(settings);
    expect(result.ok).toBe(false);
    expect(result.message.toLowerCase()).toContain("encountered an error");
  });
});



