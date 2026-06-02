import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      // Единый источник правды локалей — папка locales/ в корне репо.
      // Backend копирует её через .csproj Content include, frontend импортирует через alias.
      '@locales': path.resolve(__dirname, '../locales'),
    },
  },
  server: {
    // Разрешаем dev-серверу читать файлы выше chatbot-ui/ (нужно для @locales).
    fs: { allow: ['..'] },
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
