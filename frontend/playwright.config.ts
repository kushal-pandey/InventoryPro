import { randomUUID } from 'node:crypto'
import { existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig, devices } from '@playwright/test'

const frontendDirectory = fileURLToPath(new URL('.', import.meta.url))
const repositoryRoot = resolve(frontendDirectory, '..')
const apiPort = 5180
const frontendPort = 5181
const apiOrigin = `http://127.0.0.1:${apiPort}`
const frontendOrigin = `http://127.0.0.1:${frontendPort}`
const databasePath = resolve(tmpdir(), `inventorypro-e2e-${randomUUID()}.db`)

process.on('exit', () => {
  for (const path of [databasePath, `${databasePath}-shm`, `${databasePath}-wal`]) {
    if (existsSync(path)) rmSync(path)
  }
})

const apiEnvironment = {
  ASPNETCORE_ENVIRONMENT: 'Development',
  ASPNETCORE_URLS: apiOrigin,
  DatabaseProvider: 'Sqlite',
  ConnectionStrings__Sqlite: `Data Source=${databasePath};Default Timeout=30;Pooling=False`,
  Jwt__Key: 'InventoryPro.Playwright.E2E.Signing.Key.2026!Secure',
  Jwt__Issuer: 'InventoryPro.API.E2E',
  Jwt__Audience: 'InventoryPro.Client.E2E',
  INVENTORYPRO_ADMIN_EMAIL: 'admin@inventorypro.e2e',
  INVENTORYPRO_ADMIN_PASSWORD: 'E2E_Admin!2026_Secure',
  INVENTORYPRO_BENCHMARK_SEED: '',
  Cors__AllowedOrigins__0: frontendOrigin,
}

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'line' : 'list',
  timeout: 120_000,
  expect: { timeout: 10_000 },
  use: {
    ...devices['Desktop Chrome'],
    baseURL: frontendOrigin,
    locale: 'en-US',
    timezoneId: 'UTC',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: 'dotnet run --configuration Release --project src/InventoryPro.API --no-launch-profile',
      cwd: repositoryRoot,
      url: `${apiOrigin}/health/ready`,
      reuseExistingServer: false,
      timeout: 120_000,
      env: apiEnvironment,
    },
    {
      command: `npm run dev -- --host 127.0.0.1 --port ${frontendPort} --strictPort`,
      cwd: frontendDirectory,
      url: frontendOrigin,
      reuseExistingServer: false,
      timeout: 60_000,
      env: { ...process.env, VITE_API_BASE_URL: `${apiOrigin}/api` },
    },
  ],
})
