<script setup lang="ts">
import { computed } from 'vue'
import type { Effect } from '../../types/api'
import HighlightedText from '../HighlightedText.vue'

const props = defineProps<{
  effects: Effect[]
  query: string
}>()

const starred = computed(() => props.effects.filter((effect) => effect.star !== null))
const other = computed(() => props.effects.filter((effect) => effect.star === null))
</script>

<template>
  <section :aria-label="starred.length > 0 ? 'Legendary effects' : 'Description'">
    <h3>{{ starred.length > 0 ? 'Legendary effects' : 'Description' }}</h3>
    <ol v-if="starred.length > 0" class="stars">
      <li v-for="effect in starred" :key="`${effect.star}|${effect.text}`">
        <span class="star" :aria-label="`${effect.star} star`">{{ '★'.repeat(effect.star ?? 0) }}</span>
        <span><HighlightedText :text="effect.text" :query="query" /></span>
      </li>
    </ol>
    <ul v-if="other.length > 0" class="other" :class="{ plain: starred.length === 0 }">
      <li v-for="effect in other" :key="effect.text">
        <span v-if="starred.length > 0" class="muted">{{ effect.marker === '§' ? 'Special' : 'Note' }}</span>
        <span><HighlightedText :text="effect.text" :query="query" /></span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
h3 {
  margin-bottom: 6px;
}

ol,
ul {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 6px;
}

li {
  display: grid;
  grid-template-columns: 4.5em 1fr;
  gap: 8px;
}

.star {
  color: var(--legendary);
}

.other {
  margin-top: 8px;
}

/* A description without star effects (a legendary module) needs no label column. */
.other.plain {
  margin-top: 0;
}

.other.plain li {
  grid-template-columns: 1fr;
}
</style>
