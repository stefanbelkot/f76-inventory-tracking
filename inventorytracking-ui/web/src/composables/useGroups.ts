import { ref, watch, type Ref } from 'vue'
import { getGroups } from '../api/endpoints'
import type { CharacterEntry } from '../types/api'

/** The characters and storages; reloaded whenever the data version changes. */
export function useGroups(dataVersion: Ref<number | undefined>) {
  const characters = ref<CharacterEntry[]>([])
  const error = ref<string | null>(null)

  async function load(): Promise<void> {
    try {
      characters.value = (await getGroups()).characters
      error.value = null
    } catch (failure) {
      error.value = failure instanceof Error ? failure.message : 'The storages could not be loaded.'
    }
  }

  watch(dataVersion, (version) => version !== undefined && void load(), { immediate: true })
  return { characters, error, load }
}
