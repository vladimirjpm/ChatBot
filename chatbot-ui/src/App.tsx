import { useState } from 'react'
import { RoleSelector } from './components/RoleSelector'
import { ChatWindow } from './components/ChatWindow'
import { MessageInput } from './components/MessageInput'
import { DocumentList } from './components/DocumentList'
import { useChat, type Role, type Language } from './hooks/useChat'
import { useDocuments } from './hooks/useDocuments'

const ROLE_LABELS: Record<NonNullable<Role>, string> = {
  dotnet: '.NET / C#',
  react:  'React / TS',
  devops: 'DevOps / Cloud',
}

export default function App() {
  const [activeRole, setActiveRole] = useState<Role>(null)
  const [activeLang, setActiveLang] = useState<Language>('ru')
  const [docsOpen, setDocsOpen] = useState(false)
  const { messages, isStreaming, sendMessage, reset, sessionId } = useChat()
  const { docs, loading: docsLoading, refresh, remove } = useDocuments(sessionId)

  const handleStart = (role: Role, resumeContext: string, language: Language) => {
    setActiveRole(role)
    setActiveLang(language)
    const startMsg = language === 'en' ? "Let's begin" : 'Начнём'
    sendMessage(startMsg, role, resumeContext, language)
  }

  const handleReset = () => {
    reset()
    setActiveRole(null)
    setDocsOpen(false)
  }

  const inputPlaceholder = activeLang === 'en'
    ? 'Your answer... (Enter to send, Shift+Enter for new line)'
    : 'Твой ответ... (Enter — отправить, Shift+Enter — новая строка)'

  return (
    <div className="flex flex-col h-dvh bg-slate-900">
      {activeRole && (
        <header className="shrink-0 border-b border-slate-700/50 bg-slate-900">
          <div className="flex items-center gap-3 px-4 py-3">
            <div className="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center text-sm font-bold text-white">
              AI
            </div>
            <div className="flex-1">
              <div className="text-sm font-semibold text-white">Interview Simulator</div>
              <div className="text-xs text-slate-400">
                {ROLE_LABELS[activeRole]} · {activeLang === 'ru' ? '🇷🇺 Русский' : '🇬🇧 English'}
              </div>
            </div>

            {/* Аккордеон документов — компактная кнопка справа */}
            <button
              onClick={() => setDocsOpen(o => !o)}
              className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800 transition-colors"
            >
              📁 {activeLang === 'ru' ? 'Документы' : 'Documents'} ({docs.length})
              <span className={`transition-transform ${docsOpen ? 'rotate-180' : ''}`}>▾</span>
            </button>
          </div>

          {docsOpen && (
            <div className="px-4 pb-3 max-w-4xl mx-auto w-full">
              <DocumentList
                docs={docs}
                loading={docsLoading}
                onDelete={remove}
                emptyText={activeLang === 'ru' ? 'Документов пока нет' : 'No documents yet'}
              />
            </div>
          )}
        </header>
      )}

      {!activeRole ? (
        <RoleSelector onStart={handleStart} sessionId={sessionId} />
      ) : (
        <>
          <ChatWindow messages={messages} />
          <MessageInput
            onSend={text => sendMessage(text, activeRole, undefined, activeLang)}
            disabled={isStreaming}
            onReset={handleReset}
            placeholder={inputPlaceholder}
            sessionId={sessionId}
            onUploaded={refresh}
          />
        </>
      )}
    </div>
  )
}
