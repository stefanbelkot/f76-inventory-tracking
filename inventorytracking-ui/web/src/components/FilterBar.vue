<script setup lang="ts">
import type { ItemFilters } from '../api/query'
import type { ItemTypeInfo } from '../types/api'
import FlagFilters from './FlagFilters.vue'
import TypeChips from './TypeChips.vue'

/** The filter state is shared with the parent and edited in place. */
defineProps<{
  filters: ItemFilters
  itemTypes: ItemTypeInfo[]
  activeCount: number
}>()

defineEmits<{
  toggleType: [typeId: string]
  toggleStar: [stars: number]
  clear: []
}>()
</script>

<template>
  <section class="filters panel" aria-label="Filters">
    <div class="search">
      <input
        v-model="filters.text"
        type="search"
        placeholder="Search names and legendary effects (all words must match)"
        aria-label="Search items"
      />
      <button type="button" class="btn" :disabled="activeCount === 0" @click="$emit('clear')">
        Clear filters{{ activeCount > 0 ? ` (${activeCount})` : '' }}
      </button>
    </div>
    <TypeChips :item-types="itemTypes" :selected="filters.types" @toggle="$emit('toggleType', $event)" />
    <FlagFilters :filters="filters" @toggle-star="$emit('toggleStar', $event)" />
  </section>
</template>

<style scoped>
.filters {
  display: grid;
  gap: 8px;
  padding: 10px 12px;
}

.search {
  display: flex;
  gap: 8px;
}

.search input {
  flex: 1;
  min-width: 0;
}
</style>
