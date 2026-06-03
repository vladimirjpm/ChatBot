import type { Language } from '../hooks/useChat'
import ruRaw from '@locales/ru.json'
import enRaw from '@locales/en.json'

/**
 * UI strings come from shared JSON files /locales/{lang}.json (the same ones used by the backend).
 * Vite imports JSON directly via the @locales alias (see vite.config.ts).
 *
 * To add a language — add locales/de.json and a case in t().
 * Key parity across files is enforced by the backend unit test (LocalizationParityTests).
 */

// UI block shape is inferred from ru.json via 'as const' — TypeScript provides autocomplete.
// Requesting s.unknownKey in code produces a compile-time error.
export type Strings = typeof ruRaw.ui

const ru: Strings = ruRaw.ui
const en: Strings = enRaw.ui

export function t(lang: Language): Strings {
  switch (lang) {
    case 'en': return en
    case 'ru': return ru
    default:   return en
  }
}

/**
 * Substitutes {name}-style placeholders in a template string.
 * Used for uploadSuccessTemplate and similar patterns.
 */
export function format(template: string, args: Record<string, string | number>): string {
  return Object.entries(args).reduce(
    (acc, [k, v]) => acc.replaceAll(`{${k}}`, String(v)),
    template
  )
}
