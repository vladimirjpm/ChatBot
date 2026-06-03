import { useEffect, useRef } from 'react'
import ReactMarkdown from 'react-markdown'
import type { Message } from '../hooks/useChat'

interface Props {
  messages: Message[]
}

export function ChatWindow({ messages }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null)

  // Auto-scroll to the latest message during streaming.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  return (
    <div className="flex-1 overflow-y-auto px-4 py-6 space-y-4">
      {messages.map(msg => (
        <div
          key={msg.id}
          className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
        >
          <div
            className={`
              max-w-[80%] rounded-2xl px-4 py-3 text-sm leading-relaxed
              ${msg.role === 'user'
                ? 'bg-blue-600 text-white rounded-br-sm'
                : 'bg-slate-800 text-slate-200 rounded-bl-sm border border-slate-700'}
            `}
          >
            {msg.role === 'assistant' ? (
              <div className="prose-chat">
                <ReactMarkdown>{msg.content || ' '}</ReactMarkdown>
                {msg.streaming && (
                  <span className="inline-block w-2 h-4 bg-blue-400 ml-0.5 animate-pulse rounded-sm" />
                )}
              </div>
            ) : (
              <span>{msg.content}</span>
            )}
          </div>
        </div>
      ))}
      <div ref={bottomRef} />
    </div>
  )
}
