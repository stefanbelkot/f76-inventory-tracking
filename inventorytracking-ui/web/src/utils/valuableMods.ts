/** The mods that are NOT junk. `stars` null means the name is valuable at every star tier. */
export interface ValuableMod {
  name: string
  stars: number | null
}

const at = (stars: number | null, names: string[]): ValuableMod[] => names.map((name) => ({ name, stars }))

/**
 * Prime effects: sold for far more than junk mods (10,000 caps and up). Star tiers as seen in a real export:
 * Lucky Hit is a 3-star mod, Vital a 2-star mod, the rest 1-star.
 */
export const PRIME_MODS: ValuableMod[] = [
  ...at(1, ['Bloodied', 'Bolstering', 'Furious', "Mutant's", 'Quad', "Stalker's", 'Two Shot', 'Unyielding']),
  ...at(2, ['Vital']),
  ...at(3, ['Lucky Hit']),
]

/**
 * 4-star mods that sold at 15-45 leaders each in a PC trade channel (junk 4-star mods go for 3-7.5), so they are not junk.
 * A sample of one chat day, not complete: edit the list.
 */
export const SOUGHT_MODS: ValuableMod[] = at(4, [
  'Aegis', "Battle Loader's", "Bruiser's", "Bully's", 'Charged', "Choo-Choo's", "Combo-Breaker's", "Conductor's",
  "Electrician's", "Fencer's", "Fracturer's", "Hauler's", "Icemen's", 'Limit-Breaking', "Miasma's", "Pin-Pointer's",
  'Polished', "Pounder's", 'Raging', "Rejuvenator's", 'Reflective', 'Satiated', "Scanner's", 'Severing', "Surgeon's",
  "Tanky's", 'Tarnished', "Thrill-Seeker's", 'Vector',
])

/** Added by the owner: valuable only at the named tier (Luck, Intelligence and others exist at several tiers). */
export const EXTRA_MODS: ValuableMod[] = [
  ...at(4, ["Ranger's", "Runner's"]),
  ...at(3, ['V.A.T.S. Optimized', 'Swift']),
  ...at(1, ["Vampire's"]),
  ...at(2, ['Luck', 'Intelligence', 'Strength', 'Crippling', 'Endurance', 'Explosive', 'Powered', 'Rapid']),
]

export const PRESETS: Record<'standard' | 'prime' | 'none', ValuableMod[]> = {
  prime: PRIME_MODS,
  standard: [...PRIME_MODS, ...SOUGHT_MODS, ...EXTRA_MODS],
  none: [],
}

export const DEFAULT_VALUABLE: ValuableMod[] = PRESETS.standard.map((mod) => ({ ...mod }))

/** The first default list (every entry matched any tier); an untouched saved copy of it is upgraded to the current default. */
export const LEGACY_DEFAULT_NAMES: string[] = [
  'Bloodied', 'Bolstering', 'Furious', 'Lucky Hit', "Mutant's", 'Quad', "Stalker's", 'Two Shot', 'Unyielding', 'Vital',
  'Aegis', "Battle Loader's", "Bruiser's", "Bully's", 'Charged', "Choo-Choo's", "Combo-Breaker's", "Conductor's",
  "Electrician's", "Fencer's", "Fracturer's", "Hauler's", "Icemen's", 'Limit-Breaking', "Miasma's", "Pin-Pointer's",
  'Polished', "Pounder's", 'Raging', "Rejuvenator's", 'Reflective', 'Satiated', "Scanner's", 'Severing', "Surgeon's",
  "Tanky's", 'Tarnished', "Thrill-Seeker's", 'Vector',
]

/** "Two Shot", "4* Quad", "4★ Quad" or "Quad 4*": one mod per line (or comma); a number restricts the star tier. */
export function parseValuable(text: string): ValuableMod[] {
  const result: ValuableMod[] = []
  const seen = new Set<string>()
  for (const raw of text.split(/[\n,;]+/)) {
    let line = raw.trim()
    let stars: number | null = null
    const lead = /^(\d{1,2})\s*[*★]+\s*(.+)$/.exec(line)
    const tail = /^(.+?)\s+(\d{1,2})\s*[*★]+$/.exec(line)
    if (lead) {
      stars = Number(lead[1])
      line = lead[2]!
    } else if (tail) {
      stars = Number(tail[2])
      line = tail[1]!
    }
    line = line.replace(/^[★*\s]+/, '').trim()
    if (!line || line.length > 64 || (stars !== null && (stars < 1 || stars > 20))) continue
    const key = `${stars ?? 'any'}:${line.toLowerCase()}`
    if (seen.has(key)) continue
    seen.add(key)
    result.push({ name: line, stars })
  }
  return result
}

export function formatValuable(list: ValuableMod[]): string {
  return list.map((mod) => (mod.stars === null ? mod.name : `${mod.stars}* ${mod.name}`)).join('\n')
}

/** Same normalisation as the backend: case, punctuation and a trailing plural "s" do not matter. */
export function nameKey(name: string): string {
  const key = name.replace(/[^\p{L}\p{N}]/gu, '').toLowerCase()
  return key.length > 3 && key.endsWith('s') ? key.slice(0, -1) : key
}

/**
 * Flips one list row (a mod name at one star tier) between valuable and junk and returns the new list.
 * `tiers` are the star tiers the name exists at (needed when a valuable entry covered all tiers and only this tier turns junk).
 */
export function toggleValuable(list: ValuableMod[], name: string, stars: number, valuable: boolean, tiers: number[]): ValuableMod[] {
  const key = nameKey(name)
  if (!valuable) return [...list, { name, stars }]
  const result: ValuableMod[] = []
  for (const entry of list) {
    if (nameKey(entry.name) !== key || (entry.stars !== null && entry.stars !== stars)) {
      result.push(entry)
    } else if (entry.stars === null) {
      // An all-tier entry becomes entries for the other tiers.
      for (const tier of tiers) if (tier !== stars) result.push({ name: entry.name, stars: tier })
    }
  }
  return result
}
