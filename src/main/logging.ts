import { app } from "electron";
import { promises as fs } from "node:fs";
import path from "node:path";
import { EventEmitter } from "node:events";

export type LogLevel = "error" | "warn" | "info" | "debug";

const levelPriority: Record<LogLevel, number> = {
  error: 0,
  warn: 1,
  info: 2,
  debug: 3,
};

const emitter = new EventEmitter();
const LOG_DIR = "logs";
const LOG_FILE = "app.log";
let currentLevel: LogLevel = "info";

const resolveLogPath = (): string => {
  const userData = app?.getPath?.("userData") ?? process.cwd();
  return path.join(userData, LOG_DIR, LOG_FILE);
};

const ensureLogDir = async (): Promise<void> => {
  const dir = path.dirname(resolveLogPath());
  await fs.mkdir(dir, { recursive: true });
};

const writeLine = async (level: LogLevel, message: string): Promise<void> => {
  const timestamp = new Date().toISOString();
  const line = `[${timestamp}] [${level.toUpperCase()}] ${message}`;
  emitter.emit("log", line);
  try {
    await ensureLogDir();
    await fs.appendFile(resolveLogPath(), `${line}\n`, "utf-8");
  } catch (error) {
    console.error("Failed to write log file", error);
  }

  if (level === "error") {
    console.error(line);
  } else if (level === "warn") {
    console.warn(line);
  } else {
    console.log(line);
  }
};

const shouldLog = (level: LogLevel): boolean => levelPriority[level] <= levelPriority[currentLevel];

export const setLogLevel = (level: LogLevel): void => {
  currentLevel = level;
};

export const onLogLine = (handler: (line: string) => void): void => {
  emitter.on("log", handler);
};

export const logger = {
  error: async (message: string): Promise<void> => {
    if (shouldLog("error")) {
      await writeLine("error", message);
    }
  },
  warn: async (message: string): Promise<void> => {
    if (shouldLog("warn")) {
      await writeLine("warn", message);
    }
  },
  info: async (message: string): Promise<void> => {
    if (shouldLog("info")) {
      await writeLine("info", message);
    }
  },
  debug: async (message: string): Promise<void> => {
    if (shouldLog("debug")) {
      await writeLine("debug", message);
    }
  },
};

export const getLogFilePath = (): string => resolveLogPath();

// TODO: add log rotation / truncation strategy when file exceeds a threshold size.
