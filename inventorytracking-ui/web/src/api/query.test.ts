import { describe, expect, it } from 'vitest'
import { emptyFilters, isDescending, sortField, toQueryString, toggleSort } from './query'

describe('character filter query', () => {
  it('sends account and character, but a group wins', () => {
    const filters = { ...emptyFilters(), account: 'a b', character: 'Tester' }
    expect(toQueryString(filters)).toContain('character=Tester')
    expect(toQueryString(filters)).toContain('account=a+b')
    expect(toQueryString({ ...filters, group: 'g1' })).not.toContain('character=')
  })
})

describe('toQueryString', () => {
  it('contains only the sort for empty filters', () => {
    expect(toQueryString(emptyFilters())).toBe('sort=name')
  })

  it('writes every active filter and the paging', () => {
    const filters = {
      ...emptyFilters(),
      group: 'a-b-stash-1',
      types: ['weapon', 'armor'],
      text: '  explosive reload ',
      legendary: true,
      favorite: true,
      equipped: true,
      quest: true,
      vendor: true,
      stars: [2, 3],
      sort: '-value',
    }

    const params = new URLSearchParams(toQueryString(filters, { page: 2, pageSize: 200 }))

    expect(Object.fromEntries(params)).toEqual({
      group: 'a-b-stash-1',
      type: 'weapon,armor',
      q: 'explosive reload',
      legendary: 'true',
      favorite: 'true',
      equipped: 'true',
      quest: 'true',
      vendor: 'true',
      stars: '2,3',
      sort: '-value',
      page: '2',
      pageSize: '200',
    })
  })

  it('encodes special characters in the search text', () => {
    const filters = { ...emptyFilters(), text: 'a&b=c' }

    expect(toQueryString(filters)).toContain('q=a%26b%3Dc')
  })
})

describe('character filter query', () => {
  it('sends account and character, but a group wins', () => {
    const filters = { ...emptyFilters(), account: 'a b', character: 'Tester' }
    expect(toQueryString(filters)).toContain('character=Tester')
    expect(toQueryString(filters)).toContain('account=a+b')
    expect(toQueryString({ ...filters, group: 'g1' })).not.toContain('character=')
  })
})

describe('sort helpers', () => {
  it('toggles between ascending and descending for the same column', () => {
    expect(toggleSort('name', 'name')).toBe('-name')
    expect(toggleSort('-name', 'name')).toBe('name')
    expect(toggleSort('name', 'value')).toBe('value')
  })

  it('reads the column and direction from the sort text', () => {
    expect(sortField('-weight')).toBe('weight')
    expect(sortField('weight')).toBe('weight')
    expect(isDescending('-weight')).toBe(true)
    expect(isDescending('weight')).toBe(false)
  })
})
