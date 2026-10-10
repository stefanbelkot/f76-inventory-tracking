<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { listDirs } from '../api/endpoints'
import type { DirectoryListing } from '../types/api'

const emit = defineEmits<{ choose: [path: string] }>()

const listing = ref<DirectoryListing | null>(null)
const error = ref<string | null>(null)

async function open(path: string): Promise<void> {
  try {
    listing.value = await listDirs(path)
    error.value = null
  } catch (failure) {
    error.value = failure instanceof Error ? failure.message : 'The folder cannot be listed.'
  }
}

onMounted(() => open(''))
</script>

<template>
  <section aria-labelledby="browser-title">
    <h3 id="browser-title">Browse folders</h3>
    <p v-if="error" class="banner banner-danger" role="alert">{{ error }}</p>
    <template v-if="listing">
      <div class="bar">
        <button v-if="listing.parent !== null" type="button" class="btn" @click="open(listing.parent)">Up</button>
        <code class="current">{{ listing.path || 'Drives' }}</code>
        <button v-if="listing.path" type="button" class="btn btn-primary" @click="emit('choose', listing.path)">
          Use this folder
        </button>
      </div>
      <ul class="dirs">
        <li v-for="dir in listing.dirs" :key="dir.path">
          <button type="button" class="dir" @click="open(dir.path)">
            {{ dir.name }}
            <span v-if="dir.hasExports" class="found">has export files</span>
          </button>
        </li>
        <li v-if="listing.dirs.length === 0" class="muted">No sub-folders.</li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
.bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin: 8px 0;
}

.current {
  flex: 1;
  word-break: break-all;
}

.dirs {
  list-style: none;
  margin: 0;
  padding: 0;
  max-height: 260px;
  overflow-y: auto;
  border: 1px solid var(--border);
  border-radius: 6px;
}

.dir {
  display: flex;
  justify-content: space-between;
  width: 100%;
  padding: 6px 10px;
  border: 0;
  background: none;
  text-align: left;
}

.dir:hover {
  background: var(--surface-alt);
}

.found {
  color: var(--accent);
  font-size: 12px;
}
</style>
