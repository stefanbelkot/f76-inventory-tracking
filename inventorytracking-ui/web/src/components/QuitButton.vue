<script setup lang="ts">
import { ref } from 'vue'
import { shutdownViewer } from '../api/endpoints'
import ConfirmDialog from './ConfirmDialog.vue'

const emit = defineEmits<{ stopped: [] }>()

const asking = ref(false)
const busy = ref(false)
const error = ref<string | null>(null)

async function quit(): Promise<void> {
  busy.value = true
  try {
    await shutdownViewer()
    emit('stopped')
  } catch (failure) {
    error.value = failure instanceof Error ? failure.message : 'The viewer could not be stopped.'
    asking.value = false
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <button type="button" class="btn btn-danger" @click="asking = true">Quit</button>
  <span v-if="error" class="muted" role="alert">{{ error }}</span>
  <ConfirmDialog
    v-if="asking"
    message="Stop the viewer? Start it again to see your data."
    confirm-label="Stop the viewer"
    :busy="busy"
    @confirm="quit"
    @cancel="asking = false"
  />
</template>
