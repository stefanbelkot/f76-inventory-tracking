import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

// Development: `npm run dev` serves the UI with hot reload and forwards /api to the running .NET server.
// Start the server first, for example: dotnet run --project ../src/InventoryTracking.Server -- --no-browser --port 8080
const apiTarget = process.env.VIEWER_API ?? 'http://127.0.0.1:8080'

export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      // changeOrigin rewrites the Host header to the target, which the server's Host check requires.
      '/api': { target: apiTarget, changeOrigin: true },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
