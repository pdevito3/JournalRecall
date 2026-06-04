import { defineConfig } from 'vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import viteReact from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath } from 'node:url'

// Client-only SPA (ADR-0001). The app is served under /app, builds into the API's wwwroot/app, and
// in dev proxies /api to the .NET app so the browser always talks to a single origin (no CORS).
export default defineConfig({
  base: '/app/',
  server: {
    // Fixed custom ports so local runs are reproducible and avoid common-default collisions.
    // Aspire injects PORT for the orchestrated dev server; fall back to 4247 standalone.
    port: Number(process.env.PORT) || 4247,
    proxy: {
      '/api': { target: 'http://localhost:5247', changeOrigin: true },
    },
  },
  build: {
    outDir: fileURLToPath(new URL('../wwwroot/app', import.meta.url)),
    emptyOutDir: true,
  },
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  plugins: [
    // The router plugin generates routeTree.gen.ts and MUST come before the React plugin.
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    viteReact(),
    tailwindcss(),
  ],
})
