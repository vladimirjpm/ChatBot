import { useState, useRef, type KeyboardEvent, type ChangeEvent } from 'react'

interface Props {
  onSend: (text: string) => void
  disabled: boolean
  onReset: () => void
  placeholder?: string
}

const API_BASE = import.meta.env.VITE_API_URL ?? ''

type UploadStatus =
  | { kind: 'idle' }
  | { kind: 'uploading'; fileName: string }
  | { kind: 'success'; fileName: string; chunks: number }
  | { kind: 'error'; message: string }

export function MessageInput({ onSend, disabled, onReset, placeholder }: Props) {
  const [text, setText] = useState('')
  const [upload, setUpload] = useState<UploadStatus>({ kind: 'idle' })
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleFileChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = '' // позволяем повторно выбрать тот же файл
    if (!file) return

    setUpload({ kind: 'uploading', fileName: file.name })
    try {
      const form = new FormData()
      form.append('file', file)
      const res = await fetch(`${API_BASE}/api/documents/upload`, { method: 'POST', body: form })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data: { chunksIndexed: number; documentName: string } = await res.json()
      setUpload({ kind: 'success', fileName: data.documentName, chunks: data.chunksIndexed })
      // Автоскрытие статуса через 5 секунд
      setTimeout(() => setUpload(s => s.kind === 'success' ? { kind: 'idle' } : s), 5000)
    } catch (err) {
      setUpload({ kind: 'error', message: err instanceof Error ? err.message : 'Ошибка загрузки' })
      setTimeout(() => setUpload(s => s.kind === 'error' ? { kind: 'idle' } : s), 5000)
    }
  }

  const handleSend = () => {
    const trimmed = text.trim()
    if (!trimmed || disabled) return
    onSend(trimmed)
    setText('')
    // Сбрасываем высоту textarea после отправки
    if (textareaRef.current) textareaRef.current.style.height = 'auto'
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    // Enter отправляет, Shift+Enter — новая строка
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  const handleInput = () => {
    const el = textareaRef.current
    if (!el) return
    el.style.height = 'auto'
    el.style.height = Math.min(el.scrollHeight, 160) + 'px'
  }

  const isUploading = upload.kind === 'uploading'

  return (
    <div className="border-t border-slate-700/50 bg-slate-900 px-4 py-3">
      {/* Статус загрузки документа — компактная плашка над инпутом */}
      {upload.kind !== 'idle' && (
        <div className="max-w-4xl mx-auto mb-2 text-xs">
          {upload.kind === 'uploading' && (
            <div className="text-slate-400 flex items-center gap-2">
              <span className="w-3 h-3 border-2 border-slate-500 border-t-blue-400 rounded-full animate-spin" />
              Загружаю {upload.fileName}...
            </div>
          )}
          {upload.kind === 'success' && (
            <div className="text-green-400">
              ✓ {upload.fileName} — проиндексировано {upload.chunks} чанков
            </div>
          )}
          {upload.kind === 'error' && (
            <div className="text-red-400">⚠️ {upload.message}</div>
          )}
        </div>
      )}

      <div className="flex items-end gap-3 max-w-4xl mx-auto">
        {/* Кнопка сброса сессии */}
        <button
          onClick={onReset}
          title="Начать заново"
          className="shrink-0 w-9 h-9 rounded-lg text-slate-400 hover:text-white hover:bg-slate-700 transition-colors flex items-center justify-center text-lg"
        >
          ↺
        </button>

        {/* Загрузка документа в RAG */}
        <input
          ref={fileInputRef}
          type="file"
          accept=".pdf"
          className="hidden"
          onChange={handleFileChange}
        />
        <button
          onClick={() => fileInputRef.current?.click()}
          disabled={isUploading}
          title="Загрузить PDF в базу знаний"
          className="shrink-0 w-9 h-9 rounded-lg text-slate-400 hover:text-white hover:bg-slate-700 transition-colors flex items-center justify-center text-lg disabled:opacity-30 disabled:cursor-not-allowed"
        >
          {isUploading ? (
            <span className="w-4 h-4 border-2 border-slate-500 border-t-blue-400 rounded-full animate-spin" />
          ) : '📎'}
        </button>

        <textarea
          ref={textareaRef}
          value={text}
          onChange={e => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          onInput={handleInput}
          placeholder={placeholder ?? 'Твой ответ... (Enter — отправить)'}
          rows={1}
          disabled={disabled}
          className="flex-1 bg-slate-800 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-slate-200 placeholder-slate-500 resize-none focus:outline-none focus:border-blue-500 transition-colors disabled:opacity-50 min-h-[42px]"
        />

        <button
          onClick={handleSend}
          disabled={disabled || !text.trim()}
          className="shrink-0 w-9 h-9 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:opacity-30 disabled:cursor-not-allowed transition-all flex items-center justify-center text-white"
        >
          {disabled ? (
            <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
          ) : (
            <span className="text-base">↑</span>
          )}
        </button>
      </div>
    </div>
  )
}
