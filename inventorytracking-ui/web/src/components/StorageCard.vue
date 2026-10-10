<script setup lang="ts">
import { computed } from 'vue'
import type { ItemGroup } from '../types/api'
import { formatAge, formatDateTime, formatNumber, statusText, storageLabel } from '../utils/format'

const props = defineProps<{
  group: ItemGroup
  selected: boolean
}>()

defineEmits<{ select: [] }>()

const age = computed(() => formatAge(props.group.capturedAtUtc))
const complete = computed(() => props.group.status === 'complete')
const tooltip = computed(() => {
  const folders = props.group.folders ?? []
  return folders.length > 0 ? `${statusText(props.group)}\nRead from: ${folders.join('\n')}` : statusText(props.group)
})
</script>

<template>
  <button type="button" class="card" :aria-pressed="selected" :title="tooltip" @click="$emit('select')">
    <span class="name">{{ storageLabel(group.storage) }}</span>
    <span class="rows">{{ formatNumber(group.rows) }} rows</span>
    <span class="age muted" :title="formatDateTime(group.capturedAtUtc)">{{ age }}</span>
    <span class="status" :class="{ ok: complete }">{{ statusText(group) }}</span>
  </button>
</template>

<style scoped>
.card {
  display: grid;
  gap: 2px;
  min-width: 140px;
  padding: 8px 12px;
  text-align: left;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
}

.card:hover {
  background: var(--surface-alt);
}

.card[aria-pressed='true'] {
  border-color: var(--accent);
  background: var(--accent-soft);
}

.name {
  font-weight: 600;
}

.status {
  font-size: 12px;
  color: var(--warning);
}

.status.ok {
  color: var(--accent);
}
</style>
