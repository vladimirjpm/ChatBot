import type { DocumentInfo } from '../hooks/useDocuments'
import type { Language } from '../hooks/useChat'
import { t } from '../i18n'

interface Props {
  docs: DocumentInfo[]
  loading: boolean
  onDelete: (name: string) => void
  emptyText?: string
  lang?: Language
}

/**
 * Uploaded document list with scope badges.
 * 🔒 private — delete button (×) is shown.
 * 🌐 shared — deletion via UI is disabled (protects the shared knowledge base).
 */
export function DocumentList({ docs, loading, onDelete, emptyText, lang = 'ru' }: Props) {
  const s = t(lang)

  if (loading && docs.length === 0) {
    return <div className="text-xs text-slate-500 italic">{s.docsLoading}</div>
  }
  if (docs.length === 0) {
    return <div className="text-xs text-slate-500 italic">{emptyText ?? s.docsEmpty}</div>
  }

  return (
    <ul className="flex flex-col gap-1.5">
      {docs.map(d => (
        <li
          key={`${d.scope}-${d.name}`}
          className="flex items-center gap-2 text-xs bg-slate-800/70 rounded-lg px-3 py-1.5"
        >
          <span title={d.scope === 'private' ? s.badgePrivate : s.badgeShared}>
            {d.scope === 'private' ? '🔒' : '🌐'}
          </span>
          <span className="flex-1 text-slate-200 truncate">{d.name}</span>
          <span className="text-slate-500 shrink-0">{d.chunks} {s.chunksSuffix}</span>
          {d.scope === 'private' && (
            <button
              onClick={() => onDelete(d.name)}
              title={s.badgePrivate}
              className="shrink-0 w-5 h-5 rounded text-slate-500 hover:text-red-400 hover:bg-slate-700 transition-colors flex items-center justify-center"
            >
              ×
            </button>
          )}
        </li>
      ))}
    </ul>
  )
}
