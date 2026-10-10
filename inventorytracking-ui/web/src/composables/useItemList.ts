import { onBeforeUnmount, ref, shallowRef, watch, type Ref } from 'vue'
import { getItems } from '../api/endpoints'
import type { ItemFilters } from '../api/query'
import type { ItemSummary, ItemTotals } from '../types/api'

const PAGE_SIZE = 200
const FILTER_DELAY_MS = 120

/**
 * The filtered, sorted item list. The server filters and pages; this loads the first page when a filter or the
 * data version changes and appends further pages on demand while the table is scrolled.
 */
export function useItemList(filters: ItemFilters, dataVersion: Ref<number | undefined>) {
  const items = shallowRef<ItemSummary[]>([])
  const totalRows = ref(0)
  const totals = ref<ItemTotals | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  let generation = 0
  let nextPage = 1
  let timer: number | undefined

  async function reload(): Promise<void> {
    const current = ++generation
    loading.value = true
    try {
      const response = await getItems(filters, { page: 1, pageSize: PAGE_SIZE })
      if (current !== generation) return
      items.value = response.items
      totalRows.value = response.totalRows
      totals.value = response.totals
      nextPage = 2
      error.value = null
    } catch (failure) {
      if (current === generation) error.value = failure instanceof Error ? failure.message : 'The items could not be loaded.'
    } finally {
      if (current === generation) loading.value = false
    }
  }

  async function loadMore(): Promise<void> {
    if (loading.value || items.value.length >= totalRows.value) return
    const current = generation
    loading.value = true
    try {
      const response = await getItems(filters, { page: nextPage, pageSize: PAGE_SIZE })
      if (current !== generation) return
      items.value = [...items.value, ...response.items]
      nextPage += 1
    } catch (failure) {
      if (current === generation) error.value = failure instanceof Error ? failure.message : 'More items could not be loaded.'
    } finally {
      if (current === generation) loading.value = false
    }
  }

  function scheduleReload(): void {
    window.clearTimeout(timer)
    timer = window.setTimeout(() => void reload(), FILTER_DELAY_MS)
  }

  watch(() => ({ ...filters }), scheduleReload)
  watch(dataVersion, (version) => version !== undefined && void reload(), { immediate: true })
  onBeforeUnmount(() => window.clearTimeout(timer))

  return { items, totalRows, totals, loading, error, loadMore, reload }
}
