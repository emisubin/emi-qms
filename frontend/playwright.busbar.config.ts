import { defineConfig, devices } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e/mock-ui",
  testMatch: "interior-busbar.spec.ts",
  fullyParallel: false,
  reporter: "list",
  outputDir: "/private/tmp/emi-busbar-ui-results",
  use: { baseURL: "http://127.0.0.1:5196", ...devices["Desktop Chrome"] },
  webServer: {
    command:
      "VITE_AUTH_MODE=Dev pnpm exec vite --host 127.0.0.1 --port 5196 --strictPort",
    url: "http://127.0.0.1:5196",
    reuseExistingServer: false,
    timeout: 60_000,
  },
});
