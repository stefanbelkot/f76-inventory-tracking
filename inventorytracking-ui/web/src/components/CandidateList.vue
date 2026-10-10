<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getCandidates } from '../api/endpoints'
import type { GameCandidate } from '../types/api'
import { isSameFolder } from '../utils/folders'

const props = defineProps<{ added: string[] }>()

const emit = defineEmits<{ choose: [path: string]; chooseAll: [paths: string[]] }>()

const candidates = ref<GameCandidate[]>([])
const loaded = ref(false)

const isAdded = (path: string) => props.added.some((folder) => isSameFolder(folder, path))
const notAdded = computed(() => candidates.value.filter((candidate) => !isAdded(candidate.path)))

onMounted(async () => {
  try {
    candidates.value = (await getCandidates()).candidates
  } finally {
    loaded.value = true
  }
})
</script>

<template>
  <section aria-labelledby="candidates-title">
    <h3 id="candidates-title">Detected installs</h3>
    <p v-if="loaded && candidates.length === 0" class="muted">
      No Fallout 76 install was found automatically. Browse for the folder or type its path.
    </p>
    <template v-else>
      <ul class="list">
        <li v-for="candidate in candidates" :key="candidate.path">
          <button
            type="button"
            class="btn candidate"
            :disabled="isAdded(candidate.path)"
            @click="emit('choose', candidate.path)"
          >
            <strong>{{ candidate.label }}</strong>
            <span class="path">{{ candidate.path }}</span>
            <span class="muted">
              {{ candidate.exportFiles }} export file{{ candidate.exportFiles === 1 ? '' : 's' }}
              <template v-if="isAdded(candidate.path)"> - already added</template>
            </span>
          </button>
        </li>
      </ul>
      <button
        v-if="notAdded.length > 1"
        type="button"
        class="btn btn-primary all"
        @click="emit('chooseAll', notAdded.map((candidate) => candidate.path))"
      >
        Add all detected ({{ notAdded.length }})
      </button>
    </template>
  </section>
</template>

<style scoped>
.list {
  list-style: none;
  padding: 0;
  margin: 8px 0 0;
  display: grid;
  gap: 6px;
}

.candidate {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  width: 100%;
  text-align: left;
}

.all {
  margin-top: 8px;
}

.path {
  word-break: break-all;
}
</style>
