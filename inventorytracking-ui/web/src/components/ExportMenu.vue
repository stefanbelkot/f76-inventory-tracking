<script setup lang="ts">
import { ref } from 'vue'
import { exportView } from '../api/endpoints'
import type { ItemFilters } from '../api/query'
import { blobText, saveBlob } from '../utils/files'

const props = defineProps<{ filters: ItemFilters }>()

const message = ref<string | null>(null)

async function run(action: () => Promise<string>): Promise<void> {
  try {
    message.value = await action()
  } catch (failure) {
    message.value = failure instanceof Error ? failure.message : 'The export failed.'
  }
}

const copyList = () =>
  run(async () => {
    await navigator.clipboard.writeText(await blobText(await exportView('csv', props.filters)))
    return 'Copied the list as CSV text.'
  })

const download = (format: 'csv' | 'json') =>
  run(async () => {
    saveBlob(await exportView(format, props.filters), `inventory.${format}`)
    return `Saved inventory.${format}.`
  })
</script>

<template>
  <div class="export">
    <button type="button" class="btn" @click="copyList">Copy list</button>
    <button type="button" class="btn" @click="download('csv')">Download CSV</button>
    <button type="button" class="btn" @click="download('json')">Download JSON</button>
    <span v-if="message" class="muted" role="status">{{ message }}</span>
  </div>
</template>

<style scoped>
.export {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}
</style>
