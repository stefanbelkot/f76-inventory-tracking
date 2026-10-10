import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { getStatus } from '../api/endpoints'
import type { StatusResponse } from '../types/api'

/**
 * Polls the cheap status call; a changed data version tells the page that new export data was loaded.
 * Polling starts when `enabled` turns true (the web version first needs its session) and stops with the page.
 */
export function useStatus(enabled: Ref<boolean>, intervalMs = 3000) {
  const status = ref<StatusResponse | null>(null)
  const unreachable = ref(false)
  let timer: number | undefined

  async function refresh(): Promise<void> {
    try {
      status.value = await getStatus()
      unreachable.value = false
    } catch {
      unreachable.value = true
    }
  }

  watch(
    enabled,
    (on) => {
      window.clearInterval(timer)
      if (!on) return
      void refresh()
      timer = window.setInterval(() => void refresh(), intervalMs)
    },
    { immediate: true },
  )
  onBeforeUnmount(() => window.clearInterval(timer))

  const dataVersion = computed(() => status.value?.dataVersion)
  return { status, unreachable, dataVersion, refresh }
}
