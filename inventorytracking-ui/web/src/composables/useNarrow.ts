import { onBeforeUnmount, ref } from 'vue'

/** The width (px) below which the pages switch to the phone layout. */
export const NARROW_WIDTH = 700

/** True while the window is narrow (a phone); follows resizes and rotation. */
export function useNarrow() {
  const query = typeof window !== 'undefined' && window.matchMedia ? window.matchMedia(`(max-width: ${NARROW_WIDTH}px)`) : null
  const narrow = ref(query?.matches ?? false)
  const update = (event: MediaQueryListEvent): void => {
    narrow.value = event.matches
  }
  query?.addEventListener('change', update)
  onBeforeUnmount(() => query?.removeEventListener('change', update))
  return { narrow }
}