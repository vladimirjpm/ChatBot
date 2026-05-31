import { useState } from 'react'
import type { Role, Language } from '../hooks/useChat'

interface Props {
  onStart: (role: Role, resumeContext: string, language: Language) => void
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

export function RoleSelector({ onStart }: Props) {
  const [selectedRole, setSelectedRole] = useState<Role>(null)
  const [selectedLang, setSelectedLang] = useState<Language>('ru')
  const [resume, setResume] = useState('')

  const placeholder = selectedLang === 'ru'
    ? 'Вставь текст резюме, список технологий или описание вакансии...'
    : 'Paste your resume, tech stack, or job description...'

  const resumeLabel = selectedLang === 'ru'
    ? 'Резюме или контекст'
    : 'Resume or context'

  const resumeHint = selectedLang === 'ru'
    ? '(необязательно — бот адаптирует вопросы)'
    : '(optional — bot will tailor questions to your experience)'

  const startLabel = selectedLang === 'ru'
    ? 'Начать собеседование →'
    : 'Start interview →'

  const subtitle = selectedLang === 'ru'
    ? 'Выбери роль — и бот начнёт техническое собеседование'
    : 'Choose a role and the bot will conduct a technical interview'

  return (
    <div className="flex flex-col items-center justify-center flex-1 gap-8 px-4 py-12">
      <div className="text-center">
        <h1 className="text-3xl font-bold text-white mb-2">Interview Simulator</h1>
        <p className="text-slate-400">{subtitle}</p>
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

      {/* Выбор роли */}
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

      {/* Поле резюме */}
      <div className="w-full max-w-2xl">
        <label className="block text-sm text-slate-400 mb-2">
          {resumeLabel} <span className="text-slate-600">{resumeHint}</span>
        </label>
        <textarea
          value={resume}
          onChange={e => setResume(e.target.value)}
          placeholder={placeholder}
          rows={4}
          className="w-full bg-slate-800 border border-slate-700 rounded-xl px-4 py-3 text-sm text-slate-200 placeholder-slate-500 resize-none focus:outline-none focus:border-blue-500 transition-colors"
        />
      </div>

      <button
        onClick={() => selectedRole && onStart(selectedRole, resume, selectedLang)}
        disabled={!selectedRole}
        className="px-8 py-3 rounded-xl font-semibold text-white bg-blue-600 hover:bg-blue-500 disabled:opacity-30 disabled:cursor-not-allowed transition-all"
      >
        {startLabel}
      </button>
    </div>
  )
}
