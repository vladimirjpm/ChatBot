import { useState, useRef, type KeyboardEvent, type ChangeEvent } from 'react'
import { useDocumentUpload } from '../hooks/useDocumentUpload'

interface Props {
  onSend: (text: string) => void
  disabled: boolean
  onReset: () => void
  placeholder?: string
  sessionId: string
  onUploaded?: () => void
}

export function MessageInput({ onSend, disabled, onReset, placeholder, sessionId, onUploaded }: Props) {
  const [text, setText] = useState('')
  const { status: upload, upload: uploadFile } = useDocumentUpload()
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const sharedInputRef = useRef<HTMLInputElement>(null)
  const privateInputRef = useRef<HTMLInputElement>(null)
  const [pendingScope, setPendingScope] = useState<'shared' | 'private'>('private')

  const handleFileChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    try {
      await uploadFile(file, pendingScope, sessionId)
      onUploaded?.()
    } catch { /* status is set by the hook */ }
  }

  const openPicker = (scope: 'shared' | 'private') => {
    setPendingScope(scope)
    ;(scope === 'shared' ? sharedInputRef : privateInputRef).current?.click()
  }

  const handleSend = () => {
    const trimmed = text.trim()
    if (!trimmed || disabled) return
    onSend(trimmed)
    setText('')
    // Reset textarea height after sending.
    if (textareaRef.current) textareaRef.current.style.height = 'auto'
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    // Enter sends, Shift+Enter inserts a new line.
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
      {/* Document upload status — compact banner above the input */}
      {upload.kind !== 'idle' && (
        <div className="max-w-4xl mx-auto mb-2 text-xs">
          {upload.kind === 'uploading' && (
            <div className="text-slate-400 flex items-center gap-2">
              <span className="w-3 h-3 border-2 border-slate-500 border-t-blue-400 rounded-full animate-spin" />
              Uploading {upload.fileName}...
            </div>
          )}
          {upload.kind === 'success' && (
            <div className="text-green-400">
              ✓ {upload.fileName} — indexed {upload.chunks} chunks
            </div>
          )}
          {upload.kind === 'error' && (
            <div className="text-red-400">⚠️ {upload.message}</div>
          )}
        </div>
      )}

      <div className="flex items-end gap-3 max-w-4xl mx-auto">
        {/* Session reset button */}
        <button
          onClick={onReset}
          title="Start over"
          className="shrink-0 w-9 h-9 rounded-lg text-slate-400 hover:text-white hover:bg-slate-700 transition-colors flex items-center justify-center text-lg"
        >
          ↺
        </button>

        {/* RAG document upload — two buttons for different scopes */}
        <input ref={sharedInputRef} type="file" accept=".pdf" className="hidden" onChange={handleFileChange} />
        <input ref={privateInputRef} type="file" accept=".pdf" className="hidden" onChange={handleFileChange} />
        <button
          onClick={() => openPicker('private')}
          disabled={isUploading}
          title="🔒 Upload to this session only"
          className="shrink-0 w-9 h-9 rounded-lg text-slate-400 hover:text-blue-300 hover:bg-slate-700 transition-colors flex items-center justify-center text-base disabled:opacity-30 disabled:cursor-not-allowed"
        >
          {isUploading && pendingScope === 'private' ? (
            <span className="w-4 h-4 border-2 border-slate-500 border-t-blue-400 rounded-full animate-spin" />
          ) : '🔒'}
        </button>
        <button
          onClick={() => openPicker('shared')}
          disabled={isUploading}
          title="🌐 Upload to the shared knowledge base"
          className="shrink-0 w-9 h-9 rounded-lg text-slate-400 hover:text-white hover:bg-slate-700 transition-colors flex items-center justify-center text-base disabled:opacity-30 disabled:cursor-not-allowed"
        >
          {isUploading && pendingScope === 'shared' ? (
            <span className="w-4 h-4 border-2 border-slate-500 border-t-blue-400 rounded-full animate-spin" />
          ) : '🌐'}
        </button>

        <textarea
          ref={textareaRef}
          value={text}
          onChange={e => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          onInput={handleInput}
          placeholder={placeholder ?? 'Your answer... (Enter to send)'}
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
