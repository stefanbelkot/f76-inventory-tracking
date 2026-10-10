<script setup lang="ts">
import type { CharacterEntry, Currency } from '../types/api'
import { formatNumber } from '../utils/format'
import CurrencyList from './CurrencyList.vue'
import StorageCard from './StorageCard.vue'

defineProps<{
  characters: CharacterEntry[]
  selectedGroup: string | null
  /** The character whose storages are all selected, with its account. */
  selectedCharacter?: { account: string; character: string } | null
}>()

const emit = defineEmits<{ select: [groupId: string | null]; selectCharacter: [account: string, character: string] }>()

function totalRows(character: CharacterEntry): number {
  return character.storages.reduce((sum, group) => sum + group.rows, 0)
}

/** The currencies come with the player inventory export; the newest one that has them counts. */
function currenciesOf(character: CharacterEntry): Currency[] {
  const withCurrencies = character.storages.filter((group) => group.currencies.length > 0)
  const newest = withCurrencies.sort((a, b) => b.capturedAtUtc.localeCompare(a.capturedAtUtc))[0]
  return newest?.currencies ?? []
}
</script>

<template>
  <section class="overview" aria-label="Characters and storages">
    <button type="button" class="everything" :aria-pressed="selectedGroup === null && !selectedCharacter" @click="emit('select', null)">
      <strong>Everything</strong>
      <span class="muted">all characters and storages</span>
    </button>
    <div v-for="character in characters" :key="`${character.account}/${character.character}`" class="character">
      <h2>
        <button
          type="button"
          class="character-button"
          :aria-pressed="selectedGroup === null && selectedCharacter?.account === character.account && selectedCharacter?.character === character.character"
          title="Show all storages of this character"
          @click="emit('selectCharacter', character.account, character.character)"
        >
          {{ character.character || 'Unknown character' }}
        </button>
        <span class="muted">{{ character.account }} - {{ formatNumber(totalRows(character)) }} rows</span>
      </h2>
      <CurrencyList :currencies="currenciesOf(character)" />
      <div class="storages">
        <StorageCard
          v-for="group in character.storages"
          :key="group.id"
          :group="group"
          :selected="selectedGroup === group.id"
          @select="emit('select', group.id)"
        />
      </div>
    </div>
  </section>
</template>

<style scoped>
.overview {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: 12px 24px;
}

.everything {
  display: grid;
  padding: 8px 12px;
  text-align: left;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
}

.everything[aria-pressed='true'] {
  border-color: var(--accent);
  background: var(--accent-soft);
}

h2 {
  font-size: 14px;
  margin-bottom: 6px;
}

.character-button {
  padding: 2px 8px;
  font: inherit;
  font-weight: 600;
  border: 1px solid transparent;
  border-radius: 6px;
  background: none;
}

.character-button:hover {
  border-color: var(--border);
}

.character-button[aria-pressed='true'] {
  border-color: var(--accent);
  background: var(--accent-soft);
}

h2 .muted {
  font-weight: 400;
  margin-left: 8px;
}

.storages {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
</style>
