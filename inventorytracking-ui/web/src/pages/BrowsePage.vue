<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { toggleSort } from '../api/query'
import ExportMenu from '../components/ExportMenu.vue'
import FilterBar from '../components/FilterBar.vue'
import GroupOverview from '../components/GroupOverview.vue'
import TotalsBar from '../components/TotalsBar.vue'
import ItemDetails from '../components/details/ItemDetails.vue'
import ColumnChooser from '../components/table/ColumnChooser.vue'
import ItemTable from '../components/table/ItemTable.vue'
import { COLUMNS, visibleColumns } from '../components/table/columns'
import { useCapabilities } from '../composables/useCapabilities'
import { useGroups } from '../composables/useGroups'
import { useItemFilters } from '../composables/useItemFilters'
import { useItemList } from '../composables/useItemList'
import { useNarrow } from '../composables/useNarrow'
import { usePreferences } from '../composables/usePreferences'

const props = defineProps<{ dataVersion: number | undefined }>()

const dataVersion = computed(() => props.dataVersion)
const { itemTypes, typeNames } = useCapabilities()
const { preferences, update } = usePreferences()
const { characters, error: groupsError } = useGroups(dataVersion)
const { filters, activeCount, toggleType, toggleStar, clear } = useItemFilters()
const { items, totalRows, totals, loading, error: itemsError, loadMore } = useItemList(filters, dataVersion)

const { narrow } = useNarrow()
/** Phone layout: storages, filters and tools fold into one bar so the list keeps the screen. */
const topOpen = ref(false)
const sortable = COLUMNS.filter((column) => column.sort)
const sortField = computed(() => (filters.sort.startsWith('-') ? filters.sort.slice(1) : filters.sort))
const sortDescending = computed(() => filters.sort.startsWith('-'))

function setSortField(field: string): void {
  filters.sort = sortDescending.value ? `-${field}` : field
}

function flipSort(): void {
  filters.sort = sortDescending.value ? sortField.value : `-${sortField.value}`
}

const selectedKey = ref<string | null>(null)
const columns = computed(() => visibleColumns(preferences.value.columns, filters.group === null && filters.character === null))
const columnIds = computed(() => columns.value.map((column) => column.id))
const hasNoData = computed(() => characters.value.length === 0)

watch(
  () => [filters.group, filters.character, filters.account],
  () => (selectedKey.value = null),
)

/** One storage, or everything (null); either clears a character selection. */
function selectGroup(id: string | null): void {
  filters.group = id
  filters.account = null
  filters.character = null
}

/** All storages of one character. */
function selectCharacter(account: string, character: string): void {
  filters.group = null
  filters.account = account
  filters.character = character
}

function sortBy(field: string): void {
  filters.sort = toggleSort(filters.sort, field)
}
</script>

<template>
  <main class="browse">
    <p v-if="groupsError" class="banner banner-danger" role="alert">{{ groupsError }}</p>
    <p v-if="hasNoData" class="banner">
      No export files were found in the selected folder yet. Open your stash or inventory in the game and use the
      export button of the mod; the list appears here by itself.
    </p>

    <button
      v-if="!hasNoData && narrow"
      type="button"
      class="btn top-toggle"
      :aria-expanded="topOpen"
      @click="topOpen = !topOpen"
    >
      {{ topOpen ? "▾" : "▸" }} Storages &amp; filters<span v-if="activeCount > 0"> ({{ activeCount }} active)</span>
    </button>

    <GroupOverview v-if="!hasNoData && (!narrow || topOpen)" :characters="characters" :selected-group="filters.group"
      :selected-character="filters.character === null ? null : { account: filters.account ?? '', character: filters.character }"
      @select="selectGroup"
      @select-character="selectCharacter" />

    <FilterBar
      v-if="!hasNoData && (!narrow || topOpen)"
      :filters="filters"
      :item-types="itemTypes"
      :active-count="activeCount"
      @toggle-type="toggleType"
      @toggle-star="toggleStar"
      @clear="clear"
    />

    <div v-if="!hasNoData" class="summary">
      <TotalsBar v-if="totals" :totals="totals" :loaded="items.length" />
      <div v-if="narrow" class="summary-tools">
        <label class="sort-pick">
          Sort
          <select :value="sortField" @change="setSortField(($event.target as HTMLSelectElement).value)">
            <option v-for="column in sortable" :key="column.id" :value="column.sort">{{ column.label }}</option>
          </select>
        </label>
        <button type="button" class="btn" :aria-label="sortDescending ? 'Descending' : 'Ascending'" @click="flipSort">
          {{ sortDescending ? "▼" : "▲" }}
        </button>
      </div>
      <div v-if="!narrow || topOpen" class="summary-tools">
        <ExportMenu :filters="filters" />
        <ColumnChooser :visible="columnIds" @change="update({ columns: $event })" />
      </div>
    </div>

    <div v-if="!hasNoData" class="work" :class="{ 'with-details': selectedKey }">
      <div class="list">
        <p v-if="itemsError" class="banner banner-danger" role="alert">{{ itemsError }}</p>
        <div v-if="!loading && totalRows === 0 && !itemsError" class="empty panel">
          <p>No items match the filters.</p>
          <button v-if="activeCount > 0" type="button" class="btn" @click="clear">Clear filters</button>
        </div>
        <ItemTable
          v-else
          :items="items"
          :columns="columns"
          :sort="filters.sort"
          :selected-key="selectedKey"
          :query="filters.text"
          :type-names="typeNames"
          :cards="narrow"
          @select="selectedKey = $event"
          @sort="sortBy"
          @near-end="loadMore"
        />
      </div>
      <div v-if="selectedKey" :class="{ overlay: narrow }">
        <button v-if="narrow" type="button" class="btn back" @click="selectedKey = null">← Back to list</button>
        <ItemDetails
          :item-key="selectedKey"
          :data-version="dataVersion"
          :query="filters.text"
          :type-names="typeNames"
        />
      </div>
    </div>
  </main>
</template>

<style scoped>
.browse {
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: 100%;
  min-height: 0;
  padding: 12px 16px;
}

.summary {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 8px 16px;
}

.summary-tools {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.work {
  display: flex;
  flex: 1;
  min-height: 0;
  gap: 12px;
}

.list {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 0;
  gap: 8px;
}

.with-details > :last-child {
  flex: 0 0 380px;
  max-width: 45%;
}

.empty {
  display: grid;
  gap: 8px;
  justify-items: start;
  padding: 24px;
}

.top-toggle {
  align-self: stretch;
  text-align: left;
}

.sort-pick {
  display: flex;
  align-items: center;
  gap: 6px;
}

.overlay {
  position: fixed;
  inset: 0;
  z-index: 20;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 10px 12px;
  overflow-y: auto;
  background: var(--bg);
}

.overlay > :last-child {
  flex: 1;
}

@media (max-width: 700px) {
  .browse {
    gap: 8px;
    padding: 8px 10px;
    overflow-y: auto;
  }

  /* The list never shrinks below most of the screen; the page scrolls instead. */
  .work {
    flex: 1 0 65vh;
  }

  .with-details > :last-child {
    max-width: none;
  }
}

@media (max-width: 900px) {
  .work {
    flex-direction: column;
  }

  .with-details > :last-child {
    flex: 0 0 45%;
    max-width: none;
  }
}
</style>
