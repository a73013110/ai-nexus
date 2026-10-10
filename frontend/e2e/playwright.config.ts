import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

const desktop = {
  // Scrollbars are part of the UI and must be visible during visual QA.
  launchOptions: { ignoreDefaultArgs: ["--hide-scrollbars"] },
  viewport: { width: 1440, height: 1000 },
};

export default defineConfig({
  testDir: ".",
  testMatch: "*.spec.ts",
  fullyParallel: false,
  workers: 1,
  timeout: 30000,
  expect: { timeout: 8000 },
  outputDir: "../../artifacts/browser-results",
  reporter: [
    ["list"],
    ["html", { outputFolder: "../../artifacts/browser-report", open: "never" }],
    [
      "json",
      {
        outputFile: path.resolve(
          __dirname,
          "../../artifacts/browser-results.json",
        ),
      },
    ],
  ],
  use: {
    baseURL: "http://localhost:5180",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  // Windows verifies the browser users actually run (Edge); Linux and CI use Playwright's Chromium.
  // CHROMIUM_EXECUTABLE_PATH points at a preinstalled Chromium when the bundled revision is missing.
  projects: [
    process.platform === "win32"
      ? {
          name: "edge",
          use: { ...devices["Desktop Edge"], channel: "msedge", ...desktop },
        }
      : {
          name: "chromium",
          use: {
            ...devices["Desktop Chrome"],
            ...desktop,
            launchOptions: {
              ...desktop.launchOptions,
              executablePath: process.env.CHROMIUM_EXECUTABLE_PATH || undefined,
            },
          },
        },
  ],
  webServer: {
    command:
      "pwsh -NoProfile -File frontend/e2e/Start-BrowserTest.ps1 -Port 5180",
    cwd: path.resolve(__dirname, "../.."),
    url: "http://localhost:5180/health/live",
    timeout: 30000,
    reuseExistingServer: false,
    env: {
      ConnectionStrings__Nexus: "",
      Database__ApplyMigrationsOnStartup: "false",
      Database__User: "",
      Database__Password: "",
      Identity__ActiveDirectory__Mode: "Windows",
    },
  },
});
