<script setup lang="ts" generic="T">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

const OVERSCAN = 6
const NEAR_END_ROWS = 40

const props = defineProps<{
  items: readonly T[]
  itemHeight: number
  itemKey: (item: T) => string
}>()

const emit = defineEmits<{ nearEnd: [] }>()

defineSlots<{ default(slot: { item: T; index: number }): unknown }>()

const viewport = ref<HTMLElement | null>(null)
const scrollTop = ref(0)
const height = ref(400)
let observer: ResizeObserver | undefined

const first = computed(() => Math.max(0, Math.floor(scrollTop.value / props.itemHeight) - OVERSCAN))
const last = computed(() =>
  Math.min(props.items.length, Math.ceil((scrollTop.value + height.value) / props.itemHeight) + OVERSCAN),
)
const visible = computed(() => props.items.slice(first.value, last.value).map((item, offset) => ({ item, index: first.value + offset })))

function onScroll(): void {
  scrollTop.value = viewport.value?.scrollTop ?? 0
  if (last.value >= props.items.length - NEAR_END_ROWS) emit('nearEnd')
}

/** Scrolls just enough to bring a row into view. */
function scrollToIndex(index: number): void {
  const element = viewport.value
  if (!element) return
  const top = index * props.itemHeight
  if (top < element.scrollTop) element.scrollTop = top
  else if (top + props.itemHeight > element.scrollTop + element.clientHeight) {
    element.scrollTop = top + props.itemHeight - element.clientHeight
  }
}

onMounted(() => {
  const element = viewport.value
  if (!element) return
  height.value = element.clientHeight
  observer = new ResizeObserver(() => (height.value = element.clientHeight))
  observer.observe(element)
})
onBeforeUnmount(() => observer?.disconnect())

defineExpose({ scrollToIndex, pageSize: () => Math.max(1, Math.floor(height.value / props.itemHeight) - 1) })
</script>

<template>
  <div ref="viewport" class="viewport" @scroll.passive="onScroll">
    <div class="spacer" :style="{ height: `${items.length * itemHeight}px` }">
      <div class="window" :style="{ transform: `translateY(${first * itemHeight}px)` }">
        <template v-for="entry in visible" :key="itemKey(entry.item)">
          <slot :item="entry.item" :index="entry.index" />
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
.viewport {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.spacer {
  position: relative;
}

.window {
  position: absolute;
  inset: 0 0 auto 0;
}
</style>
