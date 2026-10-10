import { describe, expect, it } from 'vitest'
import { DEFAULT_VALUABLE, formatValuable, parseValuable, toggleValuable } from './valuableMods'

describe('parseValuable', () => {
  it('reads names with and without a star tier', () => {
    expect(parseValuable('Two Shot\n4* Quad\nVital 3★\n\n  Two Shot  ')).toEqual([
      { name: 'Two Shot', stars: null },
      { name: 'Quad', stars: 4 },
      { name: 'Vital', stars: 3 },
    ])
  })

  it('splits on commas and semicolons', () => {
    expect(parseValuable('A, B;C')).toEqual([
      { name: 'A', stars: null },
      { name: 'B', stars: null },
      { name: 'C', stars: null },
    ])
  })

  it('round-trips the default list', () => {
    expect(parseValuable(formatValuable(DEFAULT_VALUABLE))).toEqual(DEFAULT_VALUABLE)
  })
})

describe('toggleValuable', () => {
  it('adds a tier-specific entry when a mod becomes valuable', () => {
    expect(toggleValuable([], 'Luck', 3, false, [2, 3])).toEqual([{ name: 'Luck', stars: 3 }])
  })

  it('removes only that tier when a mod turns junk', () => {
    const list = [{ name: 'Luck', stars: 2 }, { name: 'Luck', stars: 3 }, { name: 'Quad', stars: 1 }]
    expect(toggleValuable(list, "luck's", 3, true, [2, 3])).toEqual([{ name: 'Luck', stars: 2 }, { name: 'Quad', stars: 1 }])
  })

  it('splits an all-tier entry into the other tiers', () => {
    expect(toggleValuable([{ name: 'Luck', stars: null }], 'Luck', 2, true, [2, 3, 4])).toEqual([
      { name: 'Luck', stars: 3 },
      { name: 'Luck', stars: 4 },
    ])
  })
})
