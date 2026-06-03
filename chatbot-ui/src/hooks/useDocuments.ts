import { useState, useEffect, useCallback } from 'react'

const API_BASE = import.meta.env.VITE_API_URL ?? ''

export interface DocumentInfo {
  name: string
  scope: 'shared' | 'private'
  chunks: number
}

/**
 * Uploaded document list with privacy awareness.
 * The backend returns shared + private documents for the current session.
 */
export function useDocuments(sessionId: string) {
  const [docs, setDocs] = useState<DocumentInfo[]>([])
  const [loading, setLoading] = useState(false)
  // bump — invalidation counter, incremented externally (after upload) to trigger a re-fetch.
  const [bump, setBump] = useState(0)

  const refresh = useCallback(() => setBump(b => b + 1), [])

  const remove = useCallback(async (name: string) => {
    const url = `${API_BASE}/api/documents/${encodeURIComponent(name)}?sessionId=${encodeURIComponent(sessionId)}`
    const res = await fetch(url, { method: 'DELETE' })
    if (!res.ok && res.status !== 404) throw new Error(`HTTP ${res.status}`)
    refresh()
  }, [sessionId, refresh])

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    fetch(`${API_BASE}/api/documents?sessionId=${encodeURIComponent(sessionId)}`)
      .then(r => r.ok ? r.json() : [])
      .then((data: DocumentInfo[]) => { if (!cancelled) setDocs(data) })
      .catch(() => { if (!cancelled) setDocs([]) })
      .finally(() => { if (!cancelled) setLoading(false) })
    return () => { cancelled = true }
  }, [sessionId, bump])

  return { docs, loading, refresh, remove }
}
