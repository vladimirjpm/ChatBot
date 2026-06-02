import { useState } from 'react'
import { RoleSelector } from './components/RoleSelector'
import { ChatWindow } from './components/ChatWindow'
import { MessageInput } from './components/MessageInput'
import { DocumentList } from './components/DocumentList'
import { useChat, type Role, type Language, type Mode } from './hooks/useChat'
import { useDocuments } from './hooks/useDocuments'
import { t } from './i18n'

const ROLE_LABELS: Record<NonNullable<Role>, string> = {
  dotnet: '.NET / C#',
  react:  'React / TS',
  devops: 'DevOps / Cloud',
}

export default function App() {
  const [activeRole, setActiveRole] = useState<Role>(null)
  const [activeLang, setActiveLang] = useState<Language>('en')
  const [activeMode, setActiveMode] = useState<Mode | null>(null)
  const [docsOpen, setDocsOpen] = useState(false)
  const { messages, isStreaming, sendMessage, reset, sessionId } = useChat()
  const { docs, loading: docsLoading, refresh, remove } = useDocuments(sessionId)

  const handleStart = (role: Role, resumeContext: string, language: Language, mode: Mode) => {
    setActiveRole(role)
    setActiveLang(language)
    setActiveMode(mode)
    if (mode === 'interview') {
      // Бот сам начинает с приветствия и первого вопроса
      const startMsg = language === 'en' ? "Let's begin" : 'Начнём'
      sendMessage(startMsg, role, resumeContext, language, mode)
    }
    // В режиме assistant ждём первого вопроса от пользователя — не шлём сообщение сами
  }

  const handleReset = () => {
    reset()
    setActiveRole(null)
    setActiveMode(null)
    setDocsOpen(false)
  }

  // В чат-режим переходим если выбран interview (с ролью) ИЛИ assistant (без роли)
  const inChat = activeMode !== null
  const s = t(activeLang)

  return (
    <div className="flex flex-col h-dvh bg-slate-900">
      {inChat && (
        <header className="shrink-0 border-b border-slate-700/50 bg-slate-900">
          <div className="flex items-center gap-3 px-4 py-3">
            <div className="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center text-sm font-bold text-white">
              AI
            </div>
            <div className="flex-1">
              <div className="text-sm font-semibold text-white">
                {activeMode === 'interview' ? s.interviewTitle : s.assistantTitle}
              </div>
              <div className="text-xs text-slate-400">
                {activeRole ? `${ROLE_LABELS[activeRole]} · ` : ''}{s.langLabel}
              </div>
            </div>

            {/* Аккордеон документов — компактная кнопка справа */}
            <button
              onClick={() => setDocsOpen(o => !o)}
              className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800 transition-colors"
            >
              📁 {s.docsHeaderChip} ({docs.length})
              <span className={`transition-transform ${docsOpen ? 'rotate-180' : ''}`}>▾</span>
            </button>
          </div>

          {docsOpen && (
            <div className="px-4 pb-3 max-w-4xl mx-auto w-full">
              <DocumentList
                docs={docs}
                loading={docsLoading}
                onDelete={remove}
                emptyText={s.docsEmpty}
                lang={activeLang}
              />
            </div>
          )}
        </header>
      )}

      {!inChat ? (
        <RoleSelector onStart={handleStart} sessionId={sessionId} />
      ) : (
        <>
          <ChatWindow messages={messages} />
          <MessageInput
            onSend={text => sendMessage(text, activeRole, undefined, activeLang, activeMode ?? 'interview')}
            disabled={isStreaming}
            onReset={handleReset}
            placeholder={s.inputPlaceholder}
            sessionId={sessionId}
            onUploaded={refresh}
          />
        </>
      )}
    </div>
  )
}
