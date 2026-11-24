import { defineConfig, devices } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import path from 'node:path';

process.env.E2E_API_KEY ||= randomBytes(32).toString('hex');
const dotnet = process.env.DOTNET_EXECUTABLE || 'dotnet';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: 'http://127.0.0.1:5091', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `"${dotnet}" run --project ../src/OutcomeGuard.Api --no-launch-profile --urls http://127.0.0.1:5091`,
    url: 'http://127.0.0.1:5091/api/config',
    reuseExistingServer: false,
    timeout: 120_000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      OutcomeGuard__ApiKey: process.env.E2E_API_KEY,
      OutcomeGuard__DemoEnabled: 'true',
      OutcomeGuard__DatabasePath: path.join(tmpdir(), `outcomeguard-e2e-${randomUUID()}.db`),
    },
  },
});
