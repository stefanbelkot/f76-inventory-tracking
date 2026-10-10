<script setup lang="ts">
import { computed, ref } from 'vue'
import { removeFolder } from '../api/endpoints'
import FolderPicker from '../components/FolderPicker.vue'
import { useCapabilities } from '../composables/useCapabilities'
import { usePreferences } from '../composables/usePreferences'
import type { StatusResponse, ThemeChoice } from '../types/api'

defineProps<{ status: StatusResponse }>()

const emit = defineEmits<{ saved: [] }>()

const removing = ref(false)
const removeError = ref<string | null>(null)

async function remove(path: string): Promise<void> {
  removing.value = true
  removeError.value = null
  try {
    await removeFolder(path)
    emit('saved')
  } catch (failure) {
    removeError.value = failure instanceof Error ? failure.message : 'The folder could not be removed.'
  } finally {
    removing.value = false
  }
}

const THEMES: { value: ThemeChoice; label: string }[] = [
  { value: 'system', label: 'Follow the system' },
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
]

const { preferences, update } = usePreferences()
const { capabilities } = useCapabilities()
const theme = computed({
  get: () => preferences.value.theme,
  set: (value: ThemeChoice) => void update({ theme: value }),
})
</script>

<template>
  <main class="settings">
    <section class="panel block">
      <h2>Export folders</h2>
      <p class="muted">
        All folders below are read together. Steam, Xbox / Game Pass and folders you copied in can be combined; the
        same file found in several folders is shown once.
        <span v-if="status.folderOrigin === 'commandLine'">
          These folders were given on the command line for this run; changing the list saves it.
        </span>
      </p>
      <ul class="folders">
        <li v-for="folder in status.folders" :key="folder.path" class="folder">
          <div class="folder-text">
            <code>{{ folder.path }}</code>
            <span v-if="!folder.exists" class="missing">Not found - is the drive connected?</span>
            <span v-else class="muted">
              {{ folder.exportFiles }} export file{{ folder.exportFiles === 1 ? '' : 's' }}
            </span>
          </div>
          <button type="button" class="btn" :disabled="removing" @click="remove(folder.path)">Remove</button>
        </li>
      </ul>
      <p v-if="removeError" class="banner banner-danger" role="alert">{{ removeError }}</p>
      <h3>Add a folder</h3>
      <FolderPicker :added="status.folders.map((folder) => folder.path)" @saved="emit('saved')" />
    </section>

    <section class="panel block">
      <h2>Appearance</h2>
      <fieldset>
        <legend class="muted">Theme</legend>
        <label v-for="option in THEMES" :key="option.value" class="option">
          <input v-model="theme" type="radio" name="theme" :value="option.value" />
          {{ option.label }}
        </label>
      </fieldset>
      <button type="button" class="btn" @click="update({ columns: null })">Reset the table columns</button>
    </section>

    <p class="muted">Inventory Tracking Viewer {{ capabilities?.version }}</p>
  </main>
</template>

<style scoped>
.settings {
  max-width: 760px;
  margin: 0 auto;
  padding: 16px;
  display: grid;
  gap: 16px;
}

.block {
  padding: 16px;
  display: grid;
  gap: 12px;
}

code {
  word-break: break-all;
}

.folders {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 6px;
}

.folder {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
}

.folder-text {
  display: grid;
  gap: 2px;
  min-width: 0;
}

.missing {
  color: var(--warning);
}

fieldset {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 16px;
  margin: 0;
  padding: 0;
  border: 0;
}

.option {
  display: flex;
  align-items: center;
  gap: 6px;
}
</style>
