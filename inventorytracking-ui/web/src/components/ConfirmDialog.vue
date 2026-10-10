<script setup lang="ts">
import { onMounted, ref } from 'vue'

defineProps<{
  message: string
  confirmLabel: string
  busy?: boolean
}>()

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()

const dialog = ref<HTMLDialogElement | null>(null)

onMounted(() => dialog.value?.showModal())
</script>

<template>
  <dialog ref="dialog" class="confirm panel" @cancel.prevent="emit('cancel')">
    <p>{{ message }}</p>
    <div class="actions">
      <button type="button" class="btn" :disabled="busy" @click="emit('cancel')">Cancel</button>
      <button type="button" class="btn btn-danger" :disabled="busy" @click="emit('confirm')">
        {{ confirmLabel }}
      </button>
    </div>
  </dialog>
</template>

<style scoped>
.confirm {
  color: var(--text);
  padding: 20px 24px;
  max-width: 420px;
  box-shadow: var(--shadow);
}

.confirm::backdrop {
  background: rgb(0 0 0 / 45%);
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 16px;
}
</style>
