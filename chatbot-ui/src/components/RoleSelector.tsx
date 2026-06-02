import { useState, useRef, type ChangeEvent } from 'react'
import type { Role, Language, Mode } from '../hooks/useChat'
import { useDocumentUpload } from '../hooks/useDocumentUpload'
import { useDocuments } from '../hooks/useDocuments'
import { DocumentList } from './DocumentList'
import { t, format } from '../i18n'

interface Props {
  onStart: (role: Role, resumeContext: string, language: Language, mode: Mode) => void
  sessionId: string
}

const ROLES: { id: Role; label: string; icon: string; desc: string }[] = [
  { id: 'dotnet', label: '.NET / C#',      icon: '⚙️', desc: 'ASP.NET Core, EF Core, SOLID, паттерны' },
  { id: 'react',  label: 'React / TS',     icon: '⚛️', desc: 'Hooks, State management, TypeScript' },
  { id: 'devops', label: 'DevOps / Cloud', icon: '☁️', desc: 'Docker, Kubernetes, CI/CD, Azure/AWS' },
]

const LANGS: { id: Language; label: string; flag: string }[] = [
  { id: 'ru', label: 'Русский', flag: '🇷🇺' },
  { id: 'en', label: 'English', flag: '🇬🇧' },
]

export function RoleSelector({ onStart, sessionId }: Props) {
  const [selectedRole, setSelectedRole] = useState<Role>(null)
  const [selectedLang, setSelectedLang] = useState<Language>('en')
  const [selectedMode, setSelectedMode] = useState<Mode>('interview')
  const [resume, setResume] = useState('')
  const sharedInputRef = useRef<HTMLInputElement>(null)
  const privateInputRef = useRef<HTMLInputElement>(null)
  const [pendingScope, setPendingScope] = useState<'shared' | 'private'>('private')
  const { status: upload, upload: uploadFile } = useDocumentUpload()
  const { docs, loading: docsLoading, refresh, remove } = useDocuments(sessionId)

  const handleFileChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    try {
      await uploadFile(file, pendingScope, sessionId)
      refresh()
    } catch { /* статус выставлен хуком */ }
  }

  const openPicker = (scope: 'shared' | 'private') => {
    setPendingScope(scope)
    ;(scope === 'shared' ? sharedInputRef : privateInputRef).current?.click()
  }

  const s = t(selectedLang)
  const isInterview = selectedMode === 'interview'
  const title = isInterview ? s.interviewTitle : s.assistantTitle
  const subtitle = isInterview ? s.interviewSubtitle : s.assistantSubtitle
  const startLabel = isInterview ? s.startInterview : s.startAssistant

  return (
    <div className="flex flex-col items-center justify-center flex-1 gap-8 px-4 py-12">
      <div className="text-center">
        <h1 className="text-3xl font-bold text-white mb-2">{title}</h1>
        <p className="text-slate-400">{subtitle}</p>
      </div>

      {/* Переключатель режима с пояснениями */}
      <div className="w-full max-w-2xl bg-slate-800/40 border border-slate-700 rounded-xl p-4">
        <div className="text-xs text-slate-400 mb-3">{s.modeLabel}</div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <label
            className={`flex gap-3 p-3 rounded-lg border cursor-pointer transition-all
              ${isInterview ? 'border-blue-500 bg-blue-950/40' : 'border-slate-700 bg-slate-900/40 hover:border-slate-500'}`}
          >
            <input type="radio" name="mode" checked={isInterview}
              onChange={() => setSelectedMode('interview')}
              className="mt-1 accent-blue-500" />
            <div>
              <div className="text-sm font-semibold text-white">{s.modeInterview}</div>
              <div className="text-xs text-slate-400 mt-1 leading-relaxed">{s.modeInterviewDesc}</div>
            </div>
          </label>

          <label
            className={`flex gap-3 p-3 rounded-lg border cursor-pointer transition-all
              ${!isInterview ? 'border-blue-500 bg-blue-950/40' : 'border-slate-700 bg-slate-900/40 hover:border-slate-500'}`}
          >
            <input type="radio" name="mode" checked={!isInterview}
              onChange={() => setSelectedMode('assistant')}
              className="mt-1 accent-blue-500" />
            <div>
              <div className="text-sm font-semibold text-white">{s.modeAssistant}</div>
              <div className="text-xs text-slate-400 mt-1 leading-relaxed">{s.modeAssistantDesc}</div>
            </div>
          </label>
        </div>
      </div>

      {/* Переключатель языка */}
      <div className="flex gap-2">
        {LANGS.map(l => (
          <button
            key={l.id}
            onClick={() => setSelectedLang(l.id)}
            className={`
              flex items-center gap-2 px-4 py-2 rounded-lg border text-sm font-medium transition-all cursor-pointer
              ${selectedLang === l.id
                ? 'border-blue-500 bg-blue-950/50 text-white'
                : 'border-slate-700 text-slate-400 hover:border-slate-500 hover:text-slate-300'}
            `}
          >
            <span>{l.flag}</span>
            <span>{l.label}</span>
          </button>
        ))}
      </div>

      {/* Выбор роли — только в режиме собеседования */}
      {isInterview && (
        <div className="flex flex-col sm:flex-row gap-4 w-full max-w-2xl">
          {ROLES.map(r => (
            <button
              key={r.id}
              onClick={() => setSelectedRole(r.id)}
              className={`
                flex-1 flex flex-col items-center gap-2 p-5 rounded-xl border-2 transition-all cursor-pointer
                ${selectedRole === r.id
                  ? 'border-blue-500 bg-blue-950/50 text-white'
                  : 'border-slate-700 bg-slate-800/50 text-slate-300 hover:border-slate-500 hover:bg-slate-800'}
              `}
            >
              <span className="text-3xl">{r.icon}</span>
              <span className="font-semibold text-sm">{r.label}</span>
              <span className="text-xs text-slate-400 text-center leading-relaxed">{r.desc}</span>
            </button>
          ))}
        </div>
      )}

      {/* Поле резюме — только в режиме interview */}
      <div className="w-full max-w-2xl">
        {isInterview && (
          <>
            <label className="block text-sm text-slate-400 mb-2">
              {s.resumeLabel} <span className="text-slate-600">{s.resumeHint}</span>
            </label>
            <textarea
              value={resume}
              onChange={e => setResume(e.target.value)}
              placeholder={s.resumePlaceholder}
              rows={4}
              className="w-full bg-slate-800 border border-slate-700 rounded-xl px-4 py-3 text-sm text-slate-200 placeholder-slate-500 resize-none focus:outline-none focus:border-blue-500 transition-colors"
            />
          </>
        )}

        {/* Загрузка PDF в RAG — два режима: shared / private */}
        <div className="mt-3 flex items-center gap-3 flex-wrap">
          <span className="text-xs text-slate-500">{s.uploadHint}</span>
          <input ref={sharedInputRef} type="file" accept=".pdf" className="hidden" onChange={handleFileChange} />
          <input ref={privateInputRef} type="file" accept=".pdf" className="hidden" onChange={handleFileChange} />

          <button
            type="button"
            onClick={() => openPicker('private')}
            disabled={upload.kind === 'uploading'}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-blue-700/60 text-blue-200 hover:border-blue-500 hover:bg-blue-900/30 transition-colors text-xs disabled:opacity-50 disabled:cursor-not-allowed"
            title={s.uploadPrivateHelp}
          >
            {s.uploadPrivate}
          </button>
          <button
            type="button"
            onClick={() => openPicker('shared')}
            disabled={upload.kind === 'uploading'}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-slate-700 text-slate-300 hover:border-slate-500 hover:text-white transition-colors text-xs disabled:opacity-50 disabled:cursor-not-allowed"
            title={s.uploadSharedHelp}
          >
            {s.uploadShared}
          </button>

          {upload.kind === 'uploading' && (
            <span className="text-xs text-slate-400 flex items-center gap-1">
              <span className="w-3 h-3 border-2 border-slate-500 border-t-blue-400 rounded-full animate-spin" />
              {upload.fileName}
            </span>
          )}
          {upload.kind === 'success' && (
            <span className="text-xs text-green-400">
              {format(s.uploadSuccessTemplate, { name: upload.fileName, chunks: upload.chunks })}
            </span>
          )}
          {upload.kind === 'error' && (
            <span className="text-xs text-red-400">⚠️ {upload.message}</span>
          )}
        </div>

        {/* Список загруженных документов */}
        <div className="mt-4">
          <div className="text-xs text-slate-400 mb-2">{s.docsTitle}</div>
          <DocumentList docs={docs} loading={docsLoading} onDelete={remove} emptyText={s.docsEmpty} lang={selectedLang} />
        </div>
      </div>

      <button
        onClick={() => {
          // В режиме assistant роль не нужна; в interview — обязательна.
          if (!isInterview) onStart(null, '', selectedLang, 'assistant')
          else if (selectedRole) onStart(selectedRole, resume, selectedLang, 'interview')
        }}
        disabled={isInterview && !selectedRole}
        className="px-8 py-3 rounded-xl font-semibold text-white bg-blue-600 hover:bg-blue-500 disabled:opacity-30 disabled:cursor-not-allowed transition-all"
      >
        {startLabel}
      </button>
    </div>
  )
}
