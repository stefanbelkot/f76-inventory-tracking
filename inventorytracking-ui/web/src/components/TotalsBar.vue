<script setup lang="ts">
import type { ItemTotals } from '../types/api'
import { formatNumber, formatWeight } from '../utils/format'

defineProps<{
  totals: ItemTotals
  loaded: number
}>()
</script>

<template>
  <dl class="totals" aria-label="Totals of the filtered list">
    <div>
      <dt>Item rows</dt>
      <dd>{{ formatNumber(totals.rows) }}<span v-if="loaded < totals.rows" class="muted"> ({{ formatNumber(loaded) }} loaded)</span></dd>
    </div>
    <div>
      <dt>Total count</dt>
      <dd>{{ formatNumber(totals.totalCount) }}</dd>
    </div>
    <div>
      <dt>Total weight</dt>
      <dd>{{ formatWeight(totals.totalWeight) }}</dd>
    </div>
    <div>
      <dt>Legendary</dt>
      <dd>{{ formatNumber(totals.legendaryCount) }}</dd>
    </div>
    <div>
      <dt>Total value</dt>
      <dd>{{ formatNumber(totals.totalValue) }}</dd>
    </div>
  </dl>
</template>

<style scoped>
.totals {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 20px;
  margin: 0;
}

.totals div {
  display: flex;
  gap: 6px;
}

dt {
  color: var(--text-muted);
}

dd {
  margin: 0;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
</style>
