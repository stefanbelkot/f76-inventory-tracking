<script setup lang="ts">
import type { ItemFilters } from '../api/query'

const STAR_CHOICES = [1, 2, 3, 4]

/** The filter state is shared with the parent and edited in place. */
const props = defineProps<{ filters: ItemFilters }>()

defineEmits<{ toggleStar: [stars: number] }>()

type FlagName = 'legendary' | 'favorite' | 'equipped' | 'quest' | 'vendor'

const FLAGS: { name: FlagName; label: string }[] = [
  { name: 'legendary', label: 'Legendary' },
  { name: 'favorite', label: 'Favorites' },
  { name: 'equipped', label: 'Equipped' },
  { name: 'quest', label: 'Quest items' },
  { name: 'vendor', label: 'For sale (player vendor)' },
]

function toggleFlag(name: FlagName): void {
  props.filters[name] = !props.filters[name]
}
</script>

<template>
  <div class="flags" role="group" aria-label="Item flags">
    <button
      v-for="flag in FLAGS"
      :key="flag.name"
      type="button"
      class="chip"
      :aria-pressed="filters[flag.name]"
      @click="toggleFlag(flag.name)"
    >
      {{ flag.label }}
    </button>
    <span class="muted">Stars</span>
    <button
      v-for="stars in STAR_CHOICES"
      :key="stars"
      type="button"
      class="chip"
      :aria-pressed="filters.stars.includes(stars)"
      :aria-label="`${stars} star${stars === 1 ? '' : 's'}`"
      @click="$emit('toggleStar', stars)"
    >
      {{ '★'.repeat(stars) }}
    </button>
  </div>
</template>

<style scoped>
.flags {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}
</style>
