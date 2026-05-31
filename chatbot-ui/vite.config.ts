import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      // Проксируем /api на бекенд — bypass нужен для SSE стриминга
      '/api': {
        target: 'http://localhost:5249',
        changeOrigin: true,
        // Отключаем буферизацию ответа чтобы SSE токены шли сразу
        configure: (proxy) => {
          proxy.on('proxyRes', (proxyRes) => {
            proxyRes.headers['x-accel-buffering'] = 'no'
          })
        },
      },
    },
  },
})
