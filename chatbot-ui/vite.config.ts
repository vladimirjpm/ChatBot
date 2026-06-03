import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      // Single source of truth for locales — the locales/ folder at the repo root.
      // The backend copies it via .csproj Content include; the frontend imports it via this alias.
      '@locales': path.resolve(__dirname, '../locales'),
    },
  },
  server: {
    // Allow the dev server to read files above chatbot-ui/ (required for @locales).
    fs: { allow: ['..'] },
    proxy: {
      // Proxy /api to the backend — bypass is needed for SSE streaming.
      '/api': {
        target: 'http://localhost:5249',
        changeOrigin: true,
        // Disable response buffering so SSE tokens are delivered immediately.
        configure: (proxy) => {
          proxy.on('proxyRes', (proxyRes) => {
            proxyRes.headers['x-accel-buffering'] = 'no'
          })
        },
      },
    },
  },
})
