import { useState, useRef, useCallback } from 'react'

export type Role = 'dotnet' | 'react' | 'devops' | null
export type Language = 'ru' | 'en'
export type Mode = 'interview' | 'assistant'

export interface Message {
  id: string
  role: 'user' | 'assistant'
  content: string
  streaming?: boolean
}

const API_BASE = import.meta.env.VITE_API_URL ?? ''

export function useChat() {
  const [messages, setMessages] = useState<Message[]>([])
  const [isStreaming, setIsStreaming] = useState(false)
  // sessionId as state so reset() triggers a re-render and recalculates the document list.
  const [sessionId, setSessionId] = useState<string>(() => crypto.randomUUID())
  const sessionIdRef = useRef<string>(sessionId)
  sessionIdRef.current = sessionId
  const abortRef = useRef<AbortController | null>(null)

  const sendMessage = useCallback(async (
    text: string,
    role: Role,
    resumeContext?: string,
    language: Language = 'ru',
    mode: Mode = 'interview'
  ) => {
    if (isStreaming || !text.trim()) return

    const userMsg: Message = { id: crypto.randomUUID(), role: 'user', content: text }
    const assistantId = crypto.randomUUID()
    const assistantMsg: Message = { id: assistantId, role: 'assistant', content: '', streaming: true }

    setMessages(prev => [...prev, userMsg, assistantMsg])
    setIsStreaming(true)

    abortRef.current = new AbortController()

    try {
      const response = await fetch(`${API_BASE}/api/chat/stream`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: text,
          sessionId: sessionIdRef.current,
          // Role, Language, ResumeContext, Mode are fixed on the backend on the first message of the session.
          role: messages.length === 0 ? role : undefined,
          resumeContext: messages.length === 0 ? resumeContext : undefined,
          language: messages.length === 0 ? language : undefined,
          mode: messages.length === 0 ? mode : undefined,
        }),
        signal: abortRef.current.signal,
      })

      if (!response.ok || !response.body) throw new Error('Network error')

      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''

      while (true) {
        const { done, value } = await reader.read()
        if (done) break

        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''

        for (const line of lines) {
          if (!line.startsWith('data: ')) continue
          const data = line.slice(6).trim()
          if (data === '[DONE]') break

          try {
            const token: string = JSON.parse(data)
            setMessages(prev => prev.map(m =>
              m.id === assistantId
                ? { ...m, content: m.content + token }
                : m
            ))
          } catch { /* ignore malformed lines */ }
        }
      }
    } catch (err: unknown) {
      if (err instanceof Error && err.name !== 'AbortError') {
        setMessages(prev => prev.map(m =>
          m.id === assistantId
            ? { ...m, content: '⚠️ Connection error. Make sure the backend is running.' }
            : m
        ))
      }
    } finally {
      setMessages(prev => prev.map(m =>
        m.id === assistantId ? { ...m, streaming: false } : m
      ))
      setIsStreaming(false)
    }
  }, [isStreaming, messages.length])

  const reset = useCallback(() => {
    abortRef.current?.abort()
    setSessionId(crypto.randomUUID())
    setMessages([])
    setIsStreaming(false)
  }, [])

  return { messages, isStreaming, sendMessage, reset, sessionId }
}
