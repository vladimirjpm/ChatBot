import type { DocumentInfo } from '../hooks/useDocuments'

interface Props {
  docs: DocumentInfo[]
  loading: boolean
  onDelete: (name: string) => void
  emptyText?: string
}

/**
 * Список загруженных документов с бейджами scope.
 * 🔒 private — есть кнопка удаления (×).
 * 🌐 shared — удаление через UI запрещено (защита общей базы знаний).
 */
export function DocumentList({ docs, loading, onDelete, emptyText }: Props) {
  if (loading && docs.length === 0) {
    return <div className="text-xs text-slate-500 italic">Загрузка списка...</div>
  }
  if (docs.length === 0) {
    return <div className="text-xs text-slate-500 italic">{emptyText ?? 'Документов пока нет'}</div>
  }

  return (
    <ul className="flex flex-col gap-1.5">
      {docs.map(d => (
        <li
          key={`${d.scope}-${d.name}`}
          className="flex items-center gap-2 text-xs bg-slate-800/70 rounded-lg px-3 py-1.5"
        >
          <span title={d.scope === 'private' ? 'Только в твоей сессии' : 'Видно всем'}>
            {d.scope === 'private' ? '🔒' : '🌐'}
          </span>
          <span className="flex-1 text-slate-200 truncate">{d.name}</span>
          <span className="text-slate-500 shrink-0">{d.chunks} chunks</span>
          {d.scope === 'private' && (
            <button
              onClick={() => onDelete(d.name)}
              title="Удалить из своей базы"
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
