import { computed, ref } from 'vue'
import { getCapabilities } from '../api/endpoints'
import type { Capabilities } from '../types/api'

const capabilities = ref<Capabilities | null>(null)

/** What the running server supports (item types, folder browsing, shutdown); loaded once. */
export function useCapabilities() {
  async function load(): Promise<void> {
    try {
      capabilities.value = await getCapabilities()
    } catch {
      // The status polling reports an unreachable server; the capabilities are retried on the next load.
    }
  }

  const itemTypes = computed(() => capabilities.value?.itemTypes ?? [])
  const typeNames = computed(() => new Map(itemTypes.value.map((type) => [type.id, type.name])))
  return { capabilities, itemTypes, typeNames, load }
}
