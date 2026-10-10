<script setup lang="ts">
import { computed, ref } from 'vue'
import { isDescending, sortField } from '../../api/query'
import type { ItemSummary } from '../../types/api'
import { gridTemplate, tableMinWidth, type ColumnDef } from './columns'
import { formatNumber, formatWeight } from '../../utils/format'
import ItemCell from './ItemCell.vue'
import VirtualList from './VirtualList.vue'
import type { VirtualListHandle } from './virtualListHandle'

const ROW_HEIGHT = 32
const CARD_HEIGHT = 58

const props = defineProps<{
  items: readonly ItemSummary[]
  columns: ColumnDef[]
  sort: string
  selectedKey: string | null
  query: string
  typeNames: Map<string, string>
  /** Phone layout: two-line cards instead of columns. */
  cards?: boolean
}>()

const emit = defineEmits<{
  select: [key: string]
  sort: [field: string]
  nearEnd: []
}>()

const list = ref<VirtualListHandle | null>(null)
const template = computed(() => gridTemplate(props.columns))
const minWidth = computed(() => (props.cards ? '0' : `${tableMinWidth(props.columns)}px`))

function ariaSort(column: ColumnDef): 'ascending' | 'descending' | 'none' {
  if (column.sort !== sortField(props.sort)) return 'none'
  return isDescending(props.sort) ? 'descending' : 'ascending'
}

function sortMark(column: ColumnDef): string {
  const state = ariaSort(column)
  return state === 'none' ? '' : state === 'ascending' ? ' ▲' : ' ▼'
}

function move(delta: number): void {
  if (props.items.length === 0) return
  const current = props.items.findIndex((item) => item.key === props.selectedKey)
  const target = Math.min(props.items.length - 1, Math.max(0, current < 0 ? 0 : current + delta))
  const item = props.items[target]
  if (!item) return
  emit('select', item.key)
  list.value?.scrollToIndex(target)
}

function onKeydown(event: KeyboardEvent): void {
  const page = list.value?.pageSize() ?? 10
  const steps: Record<string, number> = {
    ArrowDown: 1,
    ArrowUp: -1,
    PageDown: page,
    PageUp: -page,
    Home: -props.items.length,
    End: props.items.length,
  }
  const step = steps[event.key]
  if (step === undefined) return
  event.preventDefault()
  move(step)
}
</script>

<template>
  <div class="table" role="grid" :aria-rowcount="items.length" aria-label="Items" tabindex="0" @keydown="onKeydown">
    <div class="scroller">
      <div class="inner" :style="{ minWidth }">
        <div v-if="!cards" class="row head" role="row" :style="{ gridTemplateColumns: template }">
          <div v-for="column in columns" :key="column.id" role="columnheader" :aria-sort="ariaSort(column)">
            <button
              v-if="column.sort"
              type="button"
              class="sort"
              :class="{ num: column.numeric }"
              @click="emit('sort', column.sort)"
            >
              {{ column.label }}{{ sortMark(column) }}
            </button>
            <span v-else>{{ column.label }}</span>
          </div>
        </div>
        <VirtualList
          ref="list"
          :items="items"
          :item-height="cards ? CARD_HEIGHT : ROW_HEIGHT"
          :item-key="(item: ItemSummary) => item.key"
          @near-end="emit('nearEnd')"
        >
          <template #default="{ item }">
            <div
              v-if="cards"
              class="card"
              role="row"
              :aria-selected="item.key === selectedKey"
              :style="{ height: `${CARD_HEIGHT}px` }"
              @click="emit('select', item.key)"
            >
              <div class="card-top">
                <ItemCell column="name" :item="item" :query="query" :type-names="typeNames" />
                <span v-if="item.stars > 0" class="legendary">{{ '★'.repeat(item.stars) }}</span>
              </div>
              <div class="card-sub muted">
                <span>×{{ formatNumber(item.count) }}</span>
                <span>{{ formatWeight(item.totalWeight) }} wt</span>
                <span>{{ formatNumber(item.totalValue) }} caps</span>
                <span v-if="item.vendor && item.price !== null">listed {{ formatNumber(item.price) }}</span>
              </div>
            </div>
            <div
              v-else
              class="row body"
              role="row"
              :aria-selected="item.key === selectedKey"
              :style="{ gridTemplateColumns: template, height: `${ROW_HEIGHT}px` }"
              @click="emit('select', item.key)"
            >
              <div v-for="column in columns" :key="column.id" role="gridcell" class="cell">
                <ItemCell :column="column.id" :item="item" :query="query" :type-names="typeNames" />
              </div>
            </div>
          </template>
        </VirtualList>
      </div>
    </div>
  </div>
</template>

<style scoped>
.table {
  display: flex;
  flex-direction: column;
  min-height: 0;
  flex: 1;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
}

.scroller {
  flex: 1;
  min-height: 0;
  overflow-x: auto;
}

.inner {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.row {
  display: grid;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
}

.head {
  height: 34px;
  background: var(--surface-alt);
  border-bottom: 1px solid var(--border);
  font-weight: 600;
}

.sort {
  padding: 0;
  border: 0;
  background: none;
  font-weight: inherit;
}

.sort.num {
  display: block;
  width: 100%;
  text-align: right;
}

.body {
  border-bottom: 1px solid var(--border);
  cursor: default;
}

.body:hover {
  background: var(--surface-alt);
}

.body[aria-selected='true'] {
  background: var(--accent-soft);
}

.card {
  display: grid;
  align-content: center;
  gap: 2px;
  padding: 0 12px;
  border-bottom: 1px solid var(--border);
}

.card[aria-selected='true'] {
  background: var(--accent-soft);
}

.card-top {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  min-width: 0;
  font-weight: 600;
}

.card-top > :first-child {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-sub {
  display: flex;
  flex-wrap: wrap;
  gap: 0 12px;
  font-size: 12px;
}

.cell {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cell > :deep(span) {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
