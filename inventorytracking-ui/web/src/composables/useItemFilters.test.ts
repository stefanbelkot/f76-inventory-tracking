import { describe, expect, it } from 'vitest'
import { useItemFilters } from './useItemFilters'

describe('useItemFilters', () => {
  it('toggles a type on and off', () => {
    const { filters, toggleType } = useItemFilters()

    toggleType('weapon')
    toggleType('armor')
    toggleType('weapon')

    expect(filters.types).toEqual(['armor'])
  })

  it('toggles star counts', () => {
    const { filters, toggleStar } = useItemFilters()

    toggleStar(3)
    toggleStar(1)

    expect(filters.stars).toEqual([3, 1])
  })

  it('counts the active filters', () => {
    const { filters, activeCount, toggleType } = useItemFilters()
    expect(activeCount.value).toBe(0)

    toggleType('aid')
    filters.legendary = true
    filters.text = 'reload'
    filters.text = '   '

    expect(activeCount.value).toBe(2)
  })

  it('clears every filter but keeps the group and the sort', () => {
    const { filters, activeCount, toggleType, clear } = useItemFilters()
    filters.group = 'stash'
    filters.account = 'acc'
    filters.character = 'Tester'
    filters.sort = '-value'
    filters.quest = true
    toggleType('junk')

    clear()

    expect(activeCount.value).toBe(0)
    expect(filters.group).toBe('stash')
    expect([filters.account, filters.character]).toEqual(['acc', 'Tester'])
    expect(filters.sort).toBe('-value')
  })
})
