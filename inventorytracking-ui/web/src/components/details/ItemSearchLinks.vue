<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { getLinks } from '../../api/endpoints'
import type { ItemLinks } from '../../types/api'

const EDIT_DELAY_MS = 300

const props = defineProps<{ itemKey: string }>()

const links = ref<ItemLinks | null>(null)
const terms = ref('')
const error = ref<string | null>(null)
let timer: number | undefined

async function load(edited?: string): Promise<void> {
  try {
    links.value = await getLinks(props.itemKey, edited)
    if (edited === undefined) terms.value = links.value.name.terms
    error.value = null
  } catch (failure) {
    error.value = failure instanceof Error ? failure.message : 'The search links could not be built.'
  }
}

function onEdit(): void {
  window.clearTimeout(timer)
  timer = window.setTimeout(() => void load(terms.value), EDIT_DELAY_MS)
}

watch(() => props.itemKey, () => void load(), { immediate: true })
onBeforeUnmount(() => window.clearTimeout(timer))
</script>

<template>
  <section aria-label="Search the web">
    <h3>Search the web</h3>
    <p v-if="error" class="banner banner-danger" role="alert">{{ error }}</p>
    <template v-if="links">
      <label class="terms">
        <span class="muted">Search terms (edit before opening)</span>
        <input v-model="terms" type="text" @input="onEdit" />
      </label>
      <div class="buttons">
        <a class="btn" :href="links.name.googleUrl" target="_blank" rel="noopener noreferrer">Google</a>
        <a class="btn" :href="links.name.wikiUrl" target="_blank" rel="noopener noreferrer">Wiki search</a>
      </div>
      <ul v-if="links.effects.length > 0" class="effects">
        <li v-for="effect in links.effects" :key="`${effect.star}|${effect.text}`">
          <a :href="effect.googleUrl" target="_blank" rel="noopener noreferrer">
            Google: {{ '★'.repeat(effect.star) }} {{ effect.text }}
          </a>
        </li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
h3 {
  margin-bottom: 6px;
}

.terms {
  display: grid;
  gap: 4px;
}

.buttons {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

.buttons a {
  text-decoration: none;
}

.effects {
  list-style: none;
  margin: 8px 0 0;
  padding: 0;
  display: grid;
  gap: 4px;
}
</style>
