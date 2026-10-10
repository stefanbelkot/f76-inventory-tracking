import { computed, reactive } from 'vue'
import { emptyFilters, type ItemFilters } from '../api/query'

function toggled<T>(values: T[], value: T): T[] {
  return values.includes(value) ? values.filter((entry) => entry !== value) : [...values, value]
}

/** The filter state of the item list with the operations the filter controls need. */
export function useItemFilters() {
  const filters = reactive<ItemFilters>(emptyFilters())

  const activeCount = computed(
    () =>
      filters.types.length +
      filters.stars.length +
      [filters.legendary, filters.favorite, filters.equipped, filters.quest, filters.vendor, filters.text.trim() !== ''].filter(Boolean)
        .length,
  )

  function toggleType(id: string): void {
    filters.types = toggled(filters.types, id)
  }

  function toggleStar(stars: number): void {
    filters.stars = toggled(filters.stars, stars)
  }

  /** Clears every filter but keeps the selected group and the sort order. */
  function clear(): void {
    const { group, account, character, sort } = filters
    Object.assign(filters, emptyFilters(), { group, account, character, sort })
  }

  return { filters, activeCount, toggleType, toggleStar, clear }
}
