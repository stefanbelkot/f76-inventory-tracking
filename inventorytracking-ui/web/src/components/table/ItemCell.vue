<script setup lang="ts">
import { computed } from 'vue'
import type { ItemSummary } from '../../types/api'
import { formatNumber, formatWeight, starGlyphs, storageLabel } from '../../utils/format'
import HighlightedText from '../HighlightedText.vue'

const props = defineProps<{
  column: string
  item: ItemSummary
  query: string
  typeNames: Map<string, string>
}>()

const types = computed(() => props.item.types.map((id) => props.typeNames.get(id) ?? id).join(', '))

const price = computed(() => (props.item.vendor && props.item.price !== null ? formatNumber(props.item.price) : ''))
const nameTitle = computed(() =>
  props.item.vendor ? `${props.item.name} - listed at your vendor${price.value ? ` for ${price.value} caps each` : ''}` : props.item.name,
)
const priceTitle = computed(() =>
  price.value ? `Listed price per item: ${price.value} caps (${formatNumber(props.item.price! * props.item.count)} for all ${formatNumber(props.item.count)})` : '',
)
</script>

<template>
  <span v-if="column === 'name'" class="name" :class="{ legendary: item.legendary }" :title="nameTitle">
    <span v-if="item.vendor" class="vendor-badge" aria-label="Listed at your vendor">FOR SALE</span>
    <HighlightedText :text="item.name" :query="query" />
  </span>
  <span v-else-if="column === 'price'" class="num price" :title="priceTitle">{{ price }}</span>
  <span v-else-if="column === 'type'" :title="`${types} (filter ${item.filter})`">{{ types }}</span>
  <span v-else-if="column === 'count'" class="num">{{ formatNumber(item.count) }}</span>
  <span v-else-if="column === 'level'" class="num">{{ item.level }}</span>
  <span v-else-if="column === 'stars'" class="legendary">{{ starGlyphs(item.stars) }}</span>
  <span v-else-if="column === 'weight'" class="num">{{ formatWeight(item.weight) }}</span>
  <span v-else-if="column === 'totalWeight'" class="num">{{ formatWeight(item.totalWeight) }}</span>
  <span v-else-if="column === 'value'" class="num">{{ formatNumber(item.value) }}</span>
  <span v-else-if="column === 'totalValue'" class="num">{{ formatNumber(item.totalValue) }}</span>
  <span v-else-if="column === 'favorite'">{{ item.favorite ? 'Yes' : '' }}</span>
  <span v-else-if="column === 'equipped'">{{ item.equipped ? 'Yes' : '' }}</span>
  <span v-else-if="column === 'character'">{{ item.character }}</span>
  <span v-else-if="column === 'storage'">{{ storageLabel(item.storage) }}</span>
</template>

<style scoped>
.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.legendary {
  color: var(--legendary);
}

.vendor-badge {
  display: inline-block;
  margin-right: 6px;
  padding: 0 6px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.04em;
  line-height: 16px;
  color: var(--accent-text);
  background: var(--accent);
  border-radius: 4px;
  vertical-align: 1px;
}

.price {
  color: var(--accent);
  font-weight: 600;
}

.name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
