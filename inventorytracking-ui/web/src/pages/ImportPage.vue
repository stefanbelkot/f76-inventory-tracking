<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { deleteSession, deleteSessionFiles, getSession, uploadFiles } from '../api/endpoints'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import { useCapabilities } from '../composables/useCapabilities'
import { useSession } from '../composables/useSession'
import type { SessionFile, UploadResult } from '../types/api'
import { formatAge, formatDateTime } from '../utils/format'

/** The most files sent in one request; the server accepts at least this many per call. */
const BATCH = 20

const emit = defineEmits<{ changed: []; sessionChanged: [] }>()

const { capabilities } = useCapabilities()
const { sessionId, notice, startNew, shareAddress } = useSession()

const limits = computed(() => capabilities.value?.importLimits ?? null)
const files = ref<SessionFile[]>([])
const selected = ref(new Set<string>())
const results = ref<UploadResult[]>([])
const busy = ref(false)
const dragging = ref(false)
const error = ref<string | null>(null)
const copied = ref(false)
const asking = ref<'selected' | 'all' | 'session' | 'new' | null>(null)
const input = ref<HTMLInputElement | null>(null)

const allSelected = computed(() => files.value.length > 0 && selected.value.size === files.value.length)
const totalBytes = computed(() => files.value.reduce((sum, file) => sum + file.size, 0))

function kb(bytes: number): string {
  return bytes < 1024 ? `${bytes} B` : `${(bytes / 1024).toFixed(1)} KB`
}

function failure(error_: unknown, fallback: string): string {
  return error_ instanceof Error ? error_.message : fallback
}

async function load(): Promise<void> {
  try {
    files.value = (await getSession()).files
    selected.value = new Set([...selected.value].filter((name) => files.value.some((file) => file.name === name)))
  } catch (problem) {
    error.value = failure(problem, 'The file list could not be loaded.')
  }
}

async function upload(list: File[]): Promise<void> {
  if (list.length === 0 || busy.value) return
  busy.value = true
  error.value = null
  results.value = []
  try {
    const max = limits.value?.maxFileBytes ?? Number.MAX_SAFE_INTEGER
    const tooBig = list.filter((file) => file.size > max)
    const ok = list.filter((file) => file.size <= max)
    results.value = tooBig.map((file) => ({ name: file.name, accepted: false, error: `The file is larger than ${kb(max)}.` }))
    for (let start = 0; start < ok.length; start += BATCH) {
      const response = await uploadFiles(ok.slice(start, start + BATCH))
      results.value = [...results.value, ...response.results]
      files.value = response.files
    }
    emit('changed')
  } catch (problem) {
    error.value = failure(problem, 'The upload failed.')
  } finally {
    busy.value = false
    if (input.value) input.value.value = ''
  }
}

function onPick(event: Event): void {
  void upload(Array.from((event.target as HTMLInputElement).files ?? []))
}

function onDrop(event: DragEvent): void {
  dragging.value = false
  void upload(Array.from(event.dataTransfer?.files ?? []))
}

function toggle(name: string): void {
  const next = new Set(selected.value)
  if (!next.delete(name)) next.add(name)
  selected.value = next
}

function toggleAll(): void {
  selected.value = allSelected.value ? new Set() : new Set(files.value.map((file) => file.name))
}

async function remove(names: string[]): Promise<void> {
  busy.value = true
  error.value = null
  asking.value = null
  try {
    files.value = (await deleteSessionFiles(names)).files
    selected.value = new Set()
    emit('changed')
  } catch (problem) {
    error.value = failure(problem, 'The files could not be deleted.')
  } finally {
    busy.value = false
  }
}

async function removeSession(): Promise<void> {
  busy.value = true
  error.value = null
  asking.value = null
  try {
    await deleteSession()
    await startNew()
    files.value = []
    selected.value = new Set()
    results.value = []
    emit('sessionChanged')
  } catch (problem) {
    error.value = failure(problem, 'The session could not be deleted.')
  } finally {
    busy.value = false
  }
}

async function newSession(): Promise<void> {
  busy.value = true
  error.value = null
  asking.value = null
  try {
    await startNew()
    files.value = []
    selected.value = new Set()
    results.value = []
    emit('sessionChanged')
  } catch (problem) {
    error.value = failure(problem, 'A new session could not be started.')
  } finally {
    busy.value = false
  }
}

async function copyLink(): Promise<void> {
  try {
    await navigator.clipboard.writeText(shareAddress())
    copied.value = true
    window.setTimeout(() => (copied.value = false), 1500)
  } catch {
    error.value = 'The link could not be copied; select it and copy it by hand.'
  }
}

onMounted(load)
</script>

<template>
  <main class="import">
    <section class="panel block" aria-labelledby="session-title">
      <h2 id="session-title">Your session</h2>
      <p class="muted">
        Everyone who has this link sees the same files and may add or delete files. Keep it to yourself or share it with
        your friends. The link is also remembered by this browser, so opening the site again returns to it.
      </p>
      <p v-if="notice" class="banner" role="status">{{ notice }}</p>
      <div class="link">
        <input type="text" readonly :value="sessionId ? shareAddress() : ''" aria-label="Link to this session" @focus="($event.target as HTMLInputElement).select()" />
        <button type="button" class="btn" @click="copyLink">{{ copied ? 'Copied' : 'Copy link' }}</button>
      </div>
      <p v-if="limits" class="muted">
        Files are deleted {{ limits.sessionDays }} days after the session was last used. They contain your account and
        character names and are only used to show you your lists.
      </p>
    </section>

    <section class="panel block" aria-labelledby="upload-title">
      <h2 id="upload-title">Upload export files</h2>
      <p class="muted">
        Select the <code>itemdump_*.json</code> files that the mod wrote into the game's <code>modsdata</code> folder (all at
        once is fine), or drop them here. A file with the name of an uploaded one replaces it.
        <template v-if="limits">
          Up to {{ limits.maxFiles }} files, {{ kb(limits.maxFileBytes) }} each, {{ kb(limits.maxSessionBytes) }} in total.
        </template>
      </p>
      <label
        class="drop"
        :class="{ dragging }"
        @dragover.prevent="dragging = true"
        @dragleave="dragging = false"
        @drop.prevent="onDrop"
      >
        <input ref="input" type="file" multiple accept=".json,application/json" :disabled="busy" @change="onPick" />
        <span>{{ busy ? 'Working...' : 'Choose files or drop them here' }}</span>
      </label>
      <p v-if="error" class="banner banner-danger" role="alert">{{ error }}</p>
      <ul v-if="results.length > 0" class="results" aria-label="Upload results">
        <li v-for="result in results" :key="result.name" :class="result.accepted ? 'ok' : 'bad'">
          <strong>{{ result.accepted ? 'Added' : 'Refused' }}</strong>
          {{ result.name }}
          <span v-if="result.error" class="muted"> - {{ result.error }}</span>
        </li>
      </ul>
    </section>

    <section class="panel block" aria-labelledby="files-title">
      <div class="head">
        <h2 id="files-title">Files in this session ({{ files.length }}, {{ kb(totalBytes) }})</h2>
        <div class="actions">
          <button type="button" class="btn btn-danger" :disabled="busy || selected.size === 0" @click="asking = 'selected'">
            Delete selected ({{ selected.size }})
          </button>
          <button type="button" class="btn btn-danger" :disabled="busy || files.length === 0" @click="asking = 'all'">
            Delete all files
          </button>
        </div>
      </div>
      <p v-if="files.length === 0" class="muted">No files yet.</p>
      <table v-else class="files">
        <thead>
          <tr>
            <th scope="col"><input type="checkbox" :checked="allSelected" aria-label="Select all files" @change="toggleAll" /></th>
            <th scope="col">File</th>
            <th scope="col" class="num">Size</th>
            <th scope="col">Uploaded</th>
            <th scope="col"><span class="sr-only">Delete</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="file in files" :key="file.name">
            <td><input type="checkbox" :checked="selected.has(file.name)" :aria-label="`Select ${file.name}`" @change="toggle(file.name)" /></td>
            <td class="name">{{ file.name }}</td>
            <td class="num">{{ kb(file.size) }}</td>
            <td :title="formatDateTime(file.modifiedUtc)">{{ formatAge(file.modifiedUtc) }}</td>
            <td><button type="button" class="btn" :disabled="busy" @click="remove([file.name])">Delete</button></td>
          </tr>
        </tbody>
      </table>
    </section>

    <section class="panel block" aria-labelledby="manage-title">
      <h2 id="manage-title">Session</h2>
      <div class="actions">
        <button type="button" class="btn" :disabled="busy" @click="asking = 'new'">Start a new session</button>
        <button type="button" class="btn btn-danger" :disabled="busy" @click="asking = 'session'">Delete this session</button>
      </div>
      <p class="muted">
        A new session starts empty; the old one stays available through its link until it expires. Deleting a session
        removes its files at once and starts a new empty one.
      </p>
    </section>

    <ConfirmDialog
      v-if="asking === 'selected'"
      :message="`Delete ${selected.size} selected file${selected.size === 1 ? '' : 's'}?`"
      confirm-label="Delete files"
      :busy="busy"
      @confirm="remove([...selected])"
      @cancel="asking = null"
    />
    <ConfirmDialog
      v-if="asking === 'all'"
      :message="`Delete all ${files.length} files of this session?`"
      confirm-label="Delete all files"
      :busy="busy"
      @confirm="remove(files.map((file) => file.name))"
      @cancel="asking = null"
    />
    <ConfirmDialog
      v-if="asking === 'session'"
      message="Delete this session and all its files? Everyone who has the link loses the data."
      confirm-label="Delete the session"
      :busy="busy"
      @confirm="removeSession"
      @cancel="asking = null"
    />
    <ConfirmDialog
      v-if="asking === 'new'"
      message="Start a new, empty session? You can return to this one through its link."
      confirm-label="Start a new session"
      :busy="busy"
      @confirm="newSession"
      @cancel="asking = null"
    />
  </main>
</template>

<style scoped>
.import {
  max-width: 860px;
  margin: 0 auto;
  padding: 16px;
  display: grid;
  gap: 16px;
}

.block {
  padding: 16px;
  display: grid;
  gap: 10px;
}

.link {
  display: flex;
  gap: 8px;
}

.link input {
  flex: 1;
  min-width: 0;
}

.drop {
  display: grid;
  place-items: center;
  min-height: 90px;
  border: 2px dashed var(--border);
  border-radius: 8px;
  cursor: pointer;
  text-align: center;
}

.drop.dragging,
.drop:hover {
  border-color: var(--accent);
  background: var(--accent-soft);
}

.drop input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
}

.drop:focus-within {
  outline: 2px solid var(--accent);
}

.results {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 4px;
  max-height: 220px;
  overflow: auto;
}

.results .ok strong {
  color: var(--accent);
}

.results .bad strong {
  color: var(--danger, #c0392b);
}

.head {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.files {
  width: 100%;
  border-collapse: collapse;
}

.files th,
.files td {
  padding: 6px 8px;
  text-align: left;
  border-bottom: 1px solid var(--border);
}

.files .num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.files .name {
  word-break: break-all;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}

code {
  word-break: break-all;
}
</style>
