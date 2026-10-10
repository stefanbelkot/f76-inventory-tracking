<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { MANIFEST_URL, downloadUrl, formatBytes, parseManifest, type DownloadManifest } from '../utils/downloads'

const manifest = ref<DownloadManifest | null>(null)
const state = ref<'loading' | 'ready' | 'missing'>('loading')
const copied = ref<string | null>(null)

onMounted(async () => {
  try {
    const response = await fetch(MANIFEST_URL, { cache: 'no-store' })
    const parsed = response.ok ? parseManifest(await response.json()) : null
    manifest.value = parsed
    state.value = parsed && parsed.files.length > 0 ? 'ready' : 'missing'
  } catch {
    state.value = 'missing'
  }
})

async function copy(hash: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(hash)
    copied.value = hash
    window.setTimeout(() => (copied.value = null), 1500)
  } catch {
    // The hash stays visible and can be selected by hand.
  }
}
</script>

<template>
  <div>
    <p v-if="state === 'loading'" class="muted">Loading the download list...</p>
    <p v-else-if="state === 'missing'" class="banner">
      Downloads are not available on this server. Use the hosted viewer's Guide page to download the files.
    </p>
    <div v-else-if="manifest" class="scroll">
      <table class="downloads">
        <thead>
          <tr>
            <th>Download</th>
            <th>Version</th>
            <th>Size</th>
            <th>Needs</th>
            <th>SHA-256</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="file in manifest.files" :key="file.id">
            <td>
              <a class="btn btn-primary" :href="downloadUrl(file)" download>{{ file.title }}</a>
              <div v-if="file.notes" class="muted notes">{{ file.notes }}</div>
            </td>
            <td>{{ file.version }}</td>
            <td>{{ formatBytes(file.bytes) }}</td>
            <td class="muted">
              <span v-if="file.requires.game">Game {{ file.requires.game }}<br /></span>
              <span v-if="file.requires.xscal">xScal {{ file.requires.xscal }}<br /></span>
              <span v-if="file.requires.inventomatic">InventOmaticStash-UO {{ file.requires.inventomatic }}</span>
            </td>
            <td>
              <button v-if="file.sha256" type="button" class="hash" :title="file.sha256" @click="copy(file.sha256)">
                {{ copied === file.sha256 ? 'Copied' : file.sha256.slice(0, 12) + '... (copy)' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-if="manifest.updated" class="muted">List updated {{ manifest.updated }}.</p>
    </div>
  </div>
</template>

<style scoped>
.scroll {
  overflow-x: auto;
}

.downloads {
  border-collapse: collapse;
  width: 100%;
  min-width: 560px;
}

.downloads th,
.downloads td {
  padding: 8px 10px;
  border-bottom: 1px solid var(--border);
  text-align: left;
  vertical-align: top;
}

.notes {
  margin-top: 4px;
  font-size: 12px;
}

.btn {
  display: inline-block;
  text-decoration: none;
}

.hash {
  padding: 0;
  border: 0;
  background: none;
  font-family: ui-monospace, Consolas, monospace;
  font-size: 12px;
  color: var(--accent);
  cursor: pointer;
}
</style>
