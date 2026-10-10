<script setup lang="ts">
import FolderPicker from '../components/FolderPicker.vue'

const emit = defineEmits<{ saved: [] }>()
</script>

<template>
  <main class="setup">
    <h2>Where are the export files?</h2>
    <p class="muted">
      The viewer reads the export files that the InventoryTracking mod writes into the game's modsdata folder. You can
      read several folders together (Steam, Xbox / Game Pass, folders you copied in); add the first one here and more
      later under Settings. The viewer only reads the files and never changes the game files.
    </p>
    <FolderPicker :added="[]" @saved="emit('saved')" />
  </main>
</template>

<style scoped>
.setup {
  max-width: 760px;
  margin: 0 auto;
  padding: 24px 16px;
  display: grid;
  gap: 12px;
}
</style>
