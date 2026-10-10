<script setup lang="ts">
import type { ItemTypeInfo } from '../types/api'

defineProps<{
  itemTypes: ItemTypeInfo[]
  selected: string[]
}>()

defineEmits<{ toggle: [typeId: string] }>()
</script>

<template>
  <div class="chips" role="group" aria-label="Item types">
    <button
      v-for="type in itemTypes"
      :key="type.id"
      type="button"
      class="chip"
      :aria-pressed="selected.includes(type.id)"
      @click="$emit('toggle', type.id)"
    >
      {{ type.name }}
    </button>
  </div>
</template>

<style scoped>
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
</style>
