import { describe, expect, it } from 'vitest'
import { highlightParts, searchWords } from './highlight'

describe('highlightParts', () => {
  it('returns the whole text when there is no query', () => {
    expect(highlightParts('Plasma Gun', '  ')).toEqual([{ text: 'Plasma Gun', match: false }])
  })

  it('marks every occurrence of every word, ignoring case', () => {
    const parts = highlightParts('Plasma Gun of plasma', 'PLASMA gun')

    expect(parts.filter((part) => part.match).map((part) => part.text)).toEqual(['Plasma', 'Gun', 'plasma'])
    expect(parts.map((part) => part.text).join('')).toBe('Plasma Gun of plasma')
  })

  it('treats regular expression characters literally', () => {
    const parts = highlightParts('Damage (+10%) [x]', '(+10%) [x]')

    expect(parts.filter((part) => part.match).map((part) => part.text)).toEqual(['(+10%)', '[x]'])
  })

  it('splits the search text into words', () => {
    expect(searchWords('  a   b ')).toEqual(['a', 'b'])
  })
})
