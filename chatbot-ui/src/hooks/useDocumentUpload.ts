import { useState, useCallback } from 'react'

const API_BASE = import.meta.env.VITE_API_URL ?? ''

export type UploadStatus =
  | { kind: 'idle' }
  | { kind: 'uploading'; fileName: string }
  | { kind: 'success'; fileName: string; chunks: number }
  | { kind: 'error'; message: string }

/**
 * Upload a PDF to the RAG index via POST /api/documents/upload.
 * Used both on the role-selection screen and inside the chat — hence the logic is extracted into a hook.
 *
 * .NET: equivalent of a typed HttpClient service injected via DI.
 */
export function useDocumentUpload() {
  const [status, setStatus] = useState<UploadStatus>({ kind: 'idle' })

  // scope: 'shared' — visible to all, 'private' — visible only in the current session.
  const upload = useCallback(async (file: File, scope: 'shared' | 'private', sessionId?: string) => {
    setStatus({ kind: 'uploading', fileName: file.name })
    try {
      const form = new FormData()
      form.append('file', file)
      form.append('scope', scope)
      if (scope === 'private' && sessionId) form.append('sessionId', sessionId)
      const res = await fetch(`${API_BASE}/api/documents/upload`, { method: 'POST', body: form })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data: { chunksIndexed: number; documentName: string } = await res.json()
      setStatus({ kind: 'success', fileName: data.documentName, chunks: data.chunksIndexed })
      setTimeout(() => setStatus(s => s.kind === 'success' ? { kind: 'idle' } : s), 5000)
      return data
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Upload error'
      setStatus({ kind: 'error', message })
      setTimeout(() => setStatus(s => s.kind === 'error' ? { kind: 'idle' } : s), 5000)
      throw err
    }
  }, [])

  return { status, upload }
}
