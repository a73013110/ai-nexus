import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

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
    ["json", { outputFile: path.resolve(__dirname, "../../artifacts/browser-results.json") }],
  ],
  use: {
    baseURL: "http://localhost:5180",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "edge",
      use: {
        ...devices["Desktop Edge"],
        channel: "msedge",
        // Scrollbars are part of the UI and must be visible during visual QA.
        launchOptions: { ignoreDefaultArgs: ["--hide-scrollbars"] },
        viewport: { width: 1440, height: 1000 },
      },
    },
  ],
  webServer: {
    command:
      "pwsh -NoProfile -File scripts/Start-BrowserTest.ps1 -Port 5180",
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
