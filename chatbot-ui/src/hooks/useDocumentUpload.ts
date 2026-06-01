import { useState, useCallback } from 'react'

const API_BASE = import.meta.env.VITE_API_URL ?? ''

export type UploadStatus =
  | { kind: 'idle' }
  | { kind: 'uploading'; fileName: string }
  | { kind: 'success'; fileName: string; chunks: number }
  | { kind: 'error'; message: string }

/**
 * Загрузка PDF в RAG-индекс через POST /api/documents/upload.
 * Используется и на экране выбора роли, и внутри чата — поэтому логика вынесена в хук.
 *
 * .NET: эквивалент типизированного HttpClient-сервиса, инжектируемого через DI.
 */
export function useDocumentUpload() {
  const [status, setStatus] = useState<UploadStatus>({ kind: 'idle' })

  const upload = useCallback(async (file: File) => {
    setStatus({ kind: 'uploading', fileName: file.name })
    try {
      const form = new FormData()
      form.append('file', file)
      const res = await fetch(`${API_BASE}/api/documents/upload`, { method: 'POST', body: form })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data: { chunksIndexed: number; documentName: string } = await res.json()
      setStatus({ kind: 'success', fileName: data.documentName, chunks: data.chunksIndexed })
      // Автоскрытие "успеха" через 5 сек, чтобы плашка не висела вечно
      setTimeout(() => setStatus(s => s.kind === 'success' ? { kind: 'idle' } : s), 5000)
      return data
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Ошибка загрузки'
      setStatus({ kind: 'error', message })
      setTimeout(() => setStatus(s => s.kind === 'error' ? { kind: 'idle' } : s), 5000)
      throw err
    }
  }, [])

  return { status, upload }
}
