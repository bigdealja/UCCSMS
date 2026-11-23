import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Dev server proxies /api and /health to the ASP.NET Core API so the SPA and API
// share an origin in development (in production, nginx does this).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
      '/health': 'http://localhost:5080'
    }
  }
})
