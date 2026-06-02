import type { Language } from '../hooks/useChat'
import ruRaw from '@locales/ru.json'
import enRaw from '@locales/en.json'

/**
 * UI-строки берутся из общих JSON-файлов /locales/{lang}.json (тех же что использует backend).
 * Vite импортирует JSON напрямую через alias @locales (см. vite.config.ts).
 *
 * Чтобы добавить язык — положить файл locales/de.json и добавить кейс в t().
 * parity между файлами проверяется юнит-тестом на backend (LocalizationParityTests).
 */

// Структура UI-блока выводится из ru.json через 'as const' — TypeScript даёт автокомплит.
// Если в коде запросить s.unknownKey — будет compile-time ошибка.
export type Strings = typeof ruRaw.ui

const ru: Strings = ruRaw.ui
const en: Strings = enRaw.ui

export function t(lang: Language): Strings {
  switch (lang) {
    case 'en': return en
    case 'ru':
    default: return ru
  }
}

/**
 * Подстановка плейсхолдеров формата {name} в шаблонную строку.
 * Используется для uploadSuccessTemplate и подобных.
 */
export function format(template: string, args: Record<string, string | number>): string {
  return Object.entries(args).reduce(
    (acc, [k, v]) => acc.replaceAll(`{${k}}`, String(v)),
    template
  )
}
