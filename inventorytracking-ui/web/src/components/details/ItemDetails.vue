<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { getItem } from '../../api/endpoints'
import type { ItemDetail } from '../../types/api'
import { formatNumber, formatWeight, starGlyphs, storageLabel } from '../../utils/format'
import { itemToText } from '../../utils/itemText'
import EffectList from './EffectList.vue'
import ItemSearchLinks from './ItemSearchLinks.vue'

const props = defineProps<{
  itemKey: string
  dataVersion: number | undefined
  query: string
  typeNames: Map<string, string>
}>()

const item = ref<ItemDetail | null>(null)
const error = ref<string | null>(null)
const copied = ref(false)

async function load(): Promise<void> {
  try {
    item.value = await getItem(props.itemKey)
    error.value = null
  } catch (failure) {
    item.value = null
    error.value = failure instanceof Error ? failure.message : 'The item could not be loaded.'
  }
}

async function copy(): Promise<void> {
  if (!item.value) return
  await navigator.clipboard.writeText(itemToText(item.value, props.typeNames))
  copied.value = true
  window.setTimeout(() => (copied.value = false), 1500)
}

const MACHINES: Record<number, string> = {
  1: 'Vending machine',
  2: 'Display case',
  3: 'Dispenser',
  4: 'Fermenter',
  5: 'Refrigerator',
  6: 'Ally',
  7: 'Recharger',
  8: 'Freezer',
  9: 'Pet',
}

/** The CAMP machine an item is assigned to; for the vending machine this is the listed price. */
function vendorFields(value: ItemDetail): [string, string][] {
  if (value.vendor) {
    const price =
      value.price === null
        ? 'listed (no price reported)'
        : `${formatNumber(value.price)} caps each, ${formatNumber(value.price * value.count)} for all ${formatNumber(value.count)}`
    return [['Player vendor', price]]
  }
  const machine = value.vending?.machine
  return machine ? [['CAMP machine', MACHINES[machine] ?? `Machine ${machine}`]] : []
}

const fields = computed(() => {
  const value = item.value
  if (!value) return []
  const types = value.types.map((id) => props.typeNames.get(id) ?? id).join(', ')
  return [
    ['Where', `${value.character}, ${storageLabel(value.storage)}`],
    ['Count', formatNumber(value.count)],
    ['Type', `${types} (filter ${value.filter})`],
    ['Level', String(value.level)],
    ['Condition', `${value.health} / ${value.maxHealth}`],
    ['Weight', `${formatWeight(value.weight)} each, ${formatWeight(value.totalWeight)} total`],
    ['Value', `${formatNumber(value.value)} each, ${formatNumber(value.totalValue)} total`],
    ...vendorFields(value),
    ['Stars', value.stars > 0 ? `${starGlyphs(value.stars)} (${value.stars})` : '0'],
    ['Flags', [value.favorite && 'favorite', value.equipped && 'equipped', value.quest && 'quest'].filter(Boolean).join(', ') || 'none'],
    ['Card', value.card ?? 'none'],
    ['Item id', String(value.id)],
  ]
})

watch(() => [props.itemKey, props.dataVersion], load, { immediate: true })
</script>

<template>
  <aside class="details panel" aria-label="Item details">
    <p v-if="error" class="banner banner-danger" role="alert">{{ error }}</p>
    <template v-if="item">
      <header>
        <h2>{{ item.name }}</h2>
        <button type="button" class="btn" @click="copy">{{ copied ? 'Copied' : 'Copy' }}</button>
      </header>
      <dl class="fields">
        <template v-for="[label, value] in fields" :key="label">
          <dt class="muted">{{ label }}</dt>
          <dd>{{ value }}</dd>
        </template>
      </dl>
      <EffectList v-if="item.effects.length > 0" :effects="item.effects" :query="query" />
      <p v-else-if="item.card" class="muted">No description was captured for this item ({{ item.card }}).</p>
      <ItemSearchLinks :item-key="item.key" />
    </template>
  </aside>
</template>

<style scoped>
.details {
  display: grid;
  align-content: start;
  gap: 14px;
  padding: 14px 16px;
  overflow-y: auto;
}

header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
}

h2 {
  font-size: 16px;
  word-break: break-word;
}

.fields {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 2px 12px;
  margin: 0;
}

dd {
  margin: 0;
  word-break: break-word;
}
</style>
