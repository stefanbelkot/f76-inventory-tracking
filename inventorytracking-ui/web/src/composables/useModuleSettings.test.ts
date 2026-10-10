import { describe, expect, it } from 'vitest'
import { DEFAULT_SETTINGS, readSettings } from './useModuleSettings'

import { LEGACY_DEFAULT_NAMES } from '../utils/valuableMods'

describe('readSettings', () => {
  it('uses the defaults for nothing or garbage', () => {
    expect(readSettings(null)).toEqual(DEFAULT_SETTINGS())
    expect(readSettings('{nope')).toEqual(DEFAULT_SETTINGS())
  })

  it('keeps valid saved values and repairs invalid ones', () => {
    const settings = readSettings(
      JSON.stringify({ valuable: [{ name: 'Quad', stars: 9 }, { name: 5 }, { name: 'X', stars: 99 }], leadersPerModule: 0.9, method: 'perOneStar' }),
    )
    expect(settings.valuable).toEqual([{ name: 'Quad', stars: 9 }, { name: 'X', stars: null }])
    expect(settings.leadersPerModule).toBe(0.9)
    expect(settings.method).toBe('perOneStar')
  })

  it('reads the view, and the old showValuable flag', () => {
    expect(readSettings(JSON.stringify({ view: 'valuable' })).view).toBe('valuable')
    expect(readSettings(JSON.stringify({ showValuable: true })).view).toBe('all')
    expect(readSettings(JSON.stringify({ view: 'x' })).view).toBe('junk')
  })

  it('allows an empty valuable list (everything is junk)', () => {
    expect(readSettings(JSON.stringify({ valuable: [] })).valuable).toEqual([])
  })

  it('upgrades a saved copy of the old all-tier default, keeps other lists', () => {
    const old = LEGACY_DEFAULT_NAMES.map((name) => ({ name, stars: null }))
    expect(readSettings(JSON.stringify({ valuable: old })).valuable).toEqual(DEFAULT_SETTINGS().valuable)
    const edited = [...old, { name: 'Extra', stars: null }]
    expect(readSettings(JSON.stringify({ valuable: edited })).valuable).toEqual(edited)
  })

  it('the default separates star tiers of the same name', () => {
    const list = DEFAULT_SETTINGS().valuable
    expect(list).toContainEqual({ name: 'Intelligence', stars: 2 })
    expect(list).not.toContainEqual({ name: 'Intelligence', stars: 3 })
    expect(list).toContainEqual({ name: 'Lucky Hit', stars: 3 })
  })
})