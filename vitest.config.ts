import { defineConfig } from 'vitest/config';

// Unit tests for the client's data layer (src/backend). They run in Node with the
// network and SignalR faked; the end-to-end suite (vitest.e2e.config.ts) runs the
// same modules against a real API.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
    setupFiles: ['src/backend/testing/memoryStorage.ts'],
    restoreMocks: true,
  },
});
