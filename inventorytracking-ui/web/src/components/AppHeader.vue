<script setup lang="ts">
import type { View } from '../types/view'
import QuitButton from './QuitButton.vue'
import ThemeToggle from './ThemeToggle.vue'

defineProps<{
  view: View
  showNavigation: boolean
  refreshing: boolean
  /** The local folder settings page exists (local and both mode). */
  showSettings: boolean
  /** The upload page exists (web and both mode). */
  showImport: boolean
  /** The host can be stopped from the page (the Windows exe). */
  showQuit: boolean
}>()

const emit = defineEmits<{
  navigate: [view: View]
  refresh: []
  stopped: []
}>()
</script>

<template>
  <header class="header">
    <h1>Inventory Tracking Viewer</h1>
    <nav aria-label="Pages">
      <button v-if="showNavigation" type="button" class="tab" :aria-current="view === 'browse'" @click="emit('navigate', 'browse')">
        Inventory
      </button>
      <button v-if="showNavigation" type="button" class="tab" :aria-current="view === 'modules'" @click="emit('navigate', 'modules')">
        Mod calculator
      </button>
      <button type="button" class="tab" :aria-current="view === 'guide'" @click="emit('navigate', 'guide')">
        Guide
      </button>
      <button v-if="showNavigation && showSettings" type="button" class="tab" :aria-current="view === 'settings'" @click="emit('navigate', 'settings')">
        Settings
      </button>
      <button v-if="showNavigation && showImport" type="button" class="tab" :aria-current="view === 'import'" @click="emit('navigate', 'import')">
        Import
      </button>
    </nav>
    <div class="tools">
      <button v-if="showNavigation" type="button" class="btn" :disabled="refreshing" @click="emit('refresh')">
        Refresh
      </button>
      <ThemeToggle />
      <QuitButton v-if="showQuit" @stopped="emit('stopped')" />
    </div>
  </header>
</template>

<style scoped>
.header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px 24px;
  padding: 10px 16px;
  background: var(--surface);
  border-bottom: 1px solid var(--border);
}

h1 {
  font-size: 17px;
}

nav {
  display: flex;
  gap: 4px;
}

.tab {
  padding: 6px 12px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: none;
}

.tab[aria-current='true'] {
  border-bottom-color: var(--accent);
  font-weight: 600;
}

.tools {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
}
</style>
