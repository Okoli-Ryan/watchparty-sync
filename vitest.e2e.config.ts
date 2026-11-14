import { defineConfig } from 'vitest/config';

// End-to-end: the real client modules (src/backend), real SignalR and real crypto,
// against a RUNNING API. Start the API first (server/README.md), sign up two accounts,
// then run with E2E_HOST_EMAIL, E2E_VIEWER_EMAIL and E2E_PASSWORD set. VITE_API_URL
// comes from .env.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['tests/e2e/**/*.e2e.test.ts'],
    setupFiles: ['src/backend/testing/memoryStorage.ts'],
    testTimeout: 30_000,
  },
});
