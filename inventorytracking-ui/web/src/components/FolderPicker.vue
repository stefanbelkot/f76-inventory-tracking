<script setup lang="ts">
import { ref } from 'vue'
import { resolveFolder } from '../api/endpoints'
import type { FolderKind, FolderResponse } from '../types/api'
import CandidateList from './CandidateList.vue'
import FolderBrowser from './FolderBrowser.vue'

defineProps<{ added: string[] }>()

const emit = defineEmits<{ saved: [] }>()

const KIND_TEXT: Record<FolderKind, string> = {
  modsData: 'the modsdata folder',
  installFolder: 'the Data\\modsdata folder inside the given folder',
  parentFolder: 'the Content\\Data\\modsdata folder inside the given folder',
  dataFolder: 'the modsdata folder inside the given folder',
  exportFolder: 'a folder that holds export files',
}

const typedPath = ref('')
const pending = ref<FolderResponse | null>(null)
const error = ref<string | null>(null)
const saving = ref(false)

async function check(path: string): Promise<void> {
  typedPath.value = path
  pending.value = null
  error.value = null
  try {
    pending.value = await resolveFolder(path, false)
  } catch (failure) {
    error.value = failure instanceof Error ? failure.message : 'The folder could not be checked.'
  }
}

async function save(paths: string[]): Promise<void> {
  saving.value = true
  error.value = null
  try {
    for (const path of paths) await resolveFolder(path, true)
    pending.value = null
    typedPath.value = ''
    emit('saved')
  } catch (failure) {
    error.value = failure instanceof Error ? failure.message : 'The folder could not be saved.'
  } finally {
    saving.value = false
  }
}

const confirm = () => (pending.value ? save([pending.value.path]) : Promise.resolve())
</script>

<template>
  <div class="picker">
    <CandidateList :added="added" @choose="check" @choose-all="save" />

    <section aria-labelledby="typed-title">
      <h3 id="typed-title">Type or paste a path</h3>
      <p class="muted">
        The game install folder or its modsdata folder, or any folder that holds export files (for example one you copied
        from another computer).
      </p>
      <form class="typed" @submit.prevent="check(typedPath)">
        <input v-model="typedPath" type="text" aria-label="Folder path" placeholder="D:\XboxGames\Fallout 76" />
        <button type="submit" class="btn" :disabled="typedPath.trim() === ''">Check</button>
      </form>
    </section>

    <FolderBrowser @choose="check" />

    <section v-if="error" class="banner banner-danger" role="alert">{{ error }}</section>

    <section v-if="pending" class="confirm panel" aria-label="Confirm the folder">
      <p>
        Add <code>{{ pending.path }}</code>?
      </p>
      <p class="muted">
        Found {{ KIND_TEXT[pending.kind] }} with {{ pending.exportFiles }} export file{{
          pending.exportFiles === 1 ? '' : 's'
        }}.
        <template v-if="pending.exportFiles === 0">Export from the game once to see data here.</template>
      </p>
      <div class="actions">
        <button type="button" class="btn" @click="pending = null">Cancel</button>
        <button type="button" class="btn btn-primary" :disabled="saving" @click="confirm">Add this folder</button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.picker {
  display: grid;
  gap: 20px;
}

.typed {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

.typed input {
  flex: 1;
}

.confirm {
  padding: 12px 16px;
  border-color: var(--accent);
}

.confirm code {
  word-break: break-all;
}

.actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}
</style>
