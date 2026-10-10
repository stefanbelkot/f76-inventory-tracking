import { reactive, watch } from 'vue'
import { DEFAULT_VALUABLE, LEGACY_DEFAULT_NAMES, type ValuableMod } from '../utils/valuableMods'

const STORAGE_KEY = 'inventorytracking.moduleSettings'

export type PriceMethod = 'perModule' | 'perOneStar'

export type ModuleView = 'junk' | 'all' | 'valuable'

export interface ModuleSettings {
  valuable: ValuableMod[]
  /** Leaders one legendary module is worth; the page can show and edit it per module or per 1-star mod (3 modules). */
  leadersPerModule: number
  method: PriceMethod
  /** Which rows the table lists: only junk (counted), valuable ones too (marked), or only the valuable ones. */
  view: ModuleView
  /** Star tiers whose rows are folded away. */
  collapsed: number[]
}

export const DEFAULT_SETTINGS = (): ModuleSettings => ({
  valuable: DEFAULT_VALUABLE.map((mod) => ({ ...mod })),
  leadersPerModule: 0.3,
  method: 'perModule',
  view: 'junk',
  collapsed: [],
})

function number(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : fallback
}

/** An untouched copy of the first default list (all tiers) is replaced by the current default. */
function isLegacyDefault(list: ValuableMod[]): boolean {
  return list.length === LEGACY_DEFAULT_NAMES.length && list.every((mod, index) => mod.stars === null && mod.name === LEGACY_DEFAULT_NAMES[index])
}

/** Reads saved settings defensively: anything missing or malformed falls back to the default. */
export function readSettings(raw: string | null): ModuleSettings {
  const defaults = DEFAULT_SETTINGS()
  try {
    const parsed = JSON.parse(raw ?? 'null') as Partial<ModuleSettings> | null
    if (!parsed) return defaults
    const valuable = Array.isArray(parsed.valuable)
      ? parsed.valuable
          .filter((mod): mod is ValuableMod => typeof mod?.name === 'string' && mod.name.length > 0 && mod.name.length <= 64)
          .map((mod) => ({ name: mod.name, stars: typeof mod.stars === 'number' && mod.stars >= 1 && mod.stars <= 20 ? mod.stars : null }))
      : defaults.valuable
    return {
      valuable: isLegacyDefault(valuable) ? defaults.valuable : valuable,
      leadersPerModule: number(parsed.leadersPerModule, defaults.leadersPerModule),
      method: parsed.method === 'perOneStar' ? 'perOneStar' : 'perModule',
      view: ['junk', 'all', 'valuable'].includes(parsed.view as string) ? (parsed.view as ModuleView) : (parsed as { showValuable?: boolean }).showValuable ? 'all' : 'junk',
      collapsed: Array.isArray(parsed.collapsed) ? parsed.collapsed.filter((tier): tier is number => Number.isInteger(tier)) : [],
    }
  } catch {
    return defaults
  }
}

/** The calculator settings, kept in this browser's local storage. */
export function useModuleSettings() {
  let stored: string | null = null
  try {
    stored = window.localStorage.getItem(STORAGE_KEY)
  } catch {
    // Storage is blocked: the defaults apply for this page.
  }
  const settings = reactive<ModuleSettings>(readSettings(stored))
  watch(
    settings,
    () => {
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(settings))
      } catch {
        // The settings still apply for this page; they are just not remembered.
      }
    },
    { deep: true },
  )
  function reset(): void {
    Object.assign(settings, DEFAULT_SETTINGS())
  }
  return { settings, reset }
}
