import "dotenv/config";
import { app, BrowserWindow } from "electron";
import path from "node:path";
import { registerIpcHandlers } from "./ipcHandlers";
import { logger } from "./logging";

const isDev = !app.isPackaged;

const loadRenderer = async (win: BrowserWindow): Promise<void> => {
  if (isDev) {
    const devServerURL = process.env.VITE_DEV_SERVER_URL ?? "http://localhost:5173";
    await win.loadURL(devServerURL);
    win.webContents.openDevTools({ mode: "detach" });
  } else {
    const indexHtml = path.join(__dirname, "..", "renderer", "index.html");
    await win.loadFile(indexHtml);
  }
};

const createWindow = async (): Promise<void> => {
  const win = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 1200,
    minHeight: 720,
    title: "Infinium",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  await loadRenderer(win);
};

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

const startApp = async (): Promise<void> => {
  try {
    await logger.info("App ready; registering IPC handlers");
    registerIpcHandlers();
    await createWindow();

    app.on("activate", () => {
      if (BrowserWindow.getAllWindows().length === 0) {
        void createWindow();
      }
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    await logger.error(`App failed to initialize: ${message}`);
  }
};

void app.whenReady().then(startApp);
