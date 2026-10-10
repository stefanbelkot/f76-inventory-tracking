<script setup lang="ts">
import { computed } from 'vue'
import type { Currency } from '../types/api'
import { formatNumber } from '../utils/format'

const props = defineProps<{ currencies: Currency[] }>()

/** Caps are always shown; the other currencies only when the character owns some. */
const shown = computed(() => props.currencies.filter((currency) => currency.type === 0 || currency.amount > 0))

function limitText(currency: Currency): string {
  return currency.max && currency.max > 0
    ? `${currency.name}: ${formatNumber(currency.amount)} of at most ${formatNumber(currency.max)}`
    : `${currency.name}: ${formatNumber(currency.amount)}`
}
</script>

<template>
  <ul v-if="shown.length > 0" class="currencies" aria-label="Currencies">
    <li v-for="currency in shown" :key="currency.type" class="chip" :title="limitText(currency)">
      <span class="muted">{{ currency.name }}</span>
      <strong>{{ formatNumber(currency.amount) }}</strong>
    </li>
  </ul>
</template>

<style scoped>
.currencies {
  list-style: none;
  margin: 0 0 6px;
  padding: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 4px 6px;
}

.chip {
  display: flex;
  gap: 6px;
  padding: 1px 8px;
  font-size: 12px;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--surface);
}
</style>
