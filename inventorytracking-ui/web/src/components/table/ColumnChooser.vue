<script setup lang="ts">
import { ref } from 'vue'
import { COLUMNS } from './columns'

const props = defineProps<{ visible: string[] }>()

const emit = defineEmits<{ change: [columns: string[]] }>()

const open = ref(false)

function toggle(id: string): void {
  const next = props.visible.includes(id) ? props.visible.filter((column) => column !== id) : [...props.visible, id]
  emit('change', next)
}
</script>

<template>
  <div class="chooser">
    <button type="button" class="btn" :aria-expanded="open" @click="open = !open">Columns</button>
    <div v-if="open" class="menu panel" role="group" aria-label="Visible columns">
      <label v-for="column in COLUMNS" :key="column.id" class="entry">
        <input
          type="checkbox"
          :checked="visible.includes(column.id)"
          :disabled="column.id === 'name'"
          @change="toggle(column.id)"
        />
        {{ column.label }}
      </label>
    </div>
  </div>
</template>

<style scoped>
.chooser {
  position: relative;
}

.menu {
  position: absolute;
  right: 0;
  top: calc(100% + 4px);
  z-index: 5;
  display: grid;
  gap: 4px;
  padding: 10px 12px;
  box-shadow: var(--shadow);
}

.entry {
  display: flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
}
</style>
