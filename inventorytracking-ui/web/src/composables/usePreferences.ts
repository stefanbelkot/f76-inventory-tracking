import { ref, watchEffect } from 'vue'
import { getPreferences, savePreferences } from '../api/endpoints'
import type { Preferences, ThemeChoice } from '../types/api'

const STORAGE_KEY = 'inventorytracking.preferences'

const preferences = ref<Preferences>({ theme: 'system', columns: null })

/** True in the web-only version: preferences then live in this browser, not in a server file shared by all visitors. */
let browserOnly = false

export function setBrowserPreferences(on: boolean): void {
  browserOnly = on
}

/** The theme is set on the root element; "system" removes it so the browser's colour scheme decides. */
export function applyTheme(theme: ThemeChoice, root: HTMLElement = document.documentElement): void {
  if (theme === 'system') delete root.dataset.theme
  else root.dataset.theme = theme
}

function readBrowserPreferences(): Preferences | null {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? 'null') as Partial<Preferences> | null
    if (!parsed) return null
    const theme = ['system', 'light', 'dark'].includes(parsed.theme as string) ? (parsed.theme as ThemeChoice) : 'system'
    const columns = Array.isArray(parsed.columns) && parsed.columns.every((name) => typeof name === 'string') ? parsed.columns : null
    return { theme, columns }
  } catch {
    return null
  }
}

function writeBrowserPreferences(value: Preferences): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
  } catch {
    // The choice still applies for this page; it is just not remembered.
  }
}

/**
 * Theme and column layout. The local viewer saves them in its settings file; the web version is shared by many
 * visitors, so each browser keeps its own choice in its local storage.
 */
export function usePreferences() {
  async function load(inBrowser = browserOnly): Promise<void> {
    if (inBrowser) {
      preferences.value = readBrowserPreferences() ?? preferences.value
      return
    }
    try {
      preferences.value = await getPreferences()
    } catch {
      // The defaults stay in use when the settings cannot be read.
    }
  }

  async function update(change: Partial<Preferences>, inBrowser = browserOnly): Promise<void> {
    preferences.value = { ...preferences.value, ...change }
    if (inBrowser) {
      writeBrowserPreferences(preferences.value)
      return
    }
    try {
      await savePreferences(preferences.value)
    } catch {
      // The choice still applies for this session; saving it again happens with the next change.
    }
  }

  watchEffect(() => applyTheme(preferences.value.theme))
  return { preferences, load, update }
}
