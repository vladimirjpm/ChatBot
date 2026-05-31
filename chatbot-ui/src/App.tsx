import { useState } from 'react'
import { RoleSelector } from './components/RoleSelector'
import { ChatWindow } from './components/ChatWindow'
import { MessageInput } from './components/MessageInput'
import { useChat, type Role, type Language } from './hooks/useChat'

const ROLE_LABELS: Record<NonNullable<Role>, string> = {
  dotnet: '.NET / C#',
  react:  'React / TS',
  devops: 'DevOps / Cloud',
}

export default function App() {
  const [activeRole, setActiveRole] = useState<Role>(null)
  const [activeLang, setActiveLang] = useState<Language>('ru')
  const { messages, isStreaming, sendMessage, reset } = useChat()

  const handleStart = (role: Role, resumeContext: string, language: Language) => {
    setActiveRole(role)
    setActiveLang(language)
    // Первое сообщение запускает бота — он сам начнёт собеседование на выбранном языке
    const startMsg = language === 'en' ? "Let's begin" : 'Начнём'
    sendMessage(startMsg, role, resumeContext, language)
  }

  const handleReset = () => {
    reset()
    setActiveRole(null)
  }

  const inputPlaceholder = activeLang === 'en'
    ? 'Your answer... (Enter to send, Shift+Enter for new line)'
    : 'Твой ответ... (Enter — отправить, Shift+Enter — новая строка)'

  return (
    <div className="flex flex-col h-dvh bg-slate-900">
      {activeRole && (
        <header className="shrink-0 flex items-center gap-3 px-4 py-3 border-b border-slate-700/50 bg-slate-900">
          <div className="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center text-sm font-bold text-white">
            AI
          </div>
          <div>
            <div className="text-sm font-semibold text-white">Interview Simulator</div>
            <div className="text-xs text-slate-400">
              {ROLE_LABELS[activeRole]} · {activeLang === 'ru' ? '🇷🇺 Русский' : '🇬🇧 English'}
            </div>
          </div>
        </header>
      )}

      {!activeRole ? (
        <RoleSelector onStart={handleStart} />
      ) : (
        <>
          <ChatWindow messages={messages} />
          <MessageInput
            onSend={text => sendMessage(text, activeRole, undefined, activeLang)}
            disabled={isStreaming}
            onReset={handleReset}
            placeholder={inputPlaceholder}
          />
        </>
      )}
    </div>
  )
}
