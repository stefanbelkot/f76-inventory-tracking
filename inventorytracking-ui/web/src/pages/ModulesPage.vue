<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { getModuleSummary } from '../api/endpoints'
import { useGroups } from '../composables/useGroups'
import { useModuleSettings } from '../composables/useModuleSettings'
import type { ModuleRow, ModuleSummary, StarTier } from '../types/api'
import { formatNumber } from '../utils/format'
import { PRESETS, toggleValuable } from '../utils/valuableMods'

const props = defineProps<{ dataVersion: number | undefined }>()

const dataVersion = computed(() => props.dataVersion)
const { characters, error: groupsError } = useGroups(dataVersion)
const { settings, reset } = useModuleSettings()

const group = ref<string | null>(null)
const summary = ref<ModuleSummary | null>(null)
const error = ref<string | null>(null)
const copied = ref(false)

const groups = computed(() =>
  characters.value.flatMap((character) =>
    character.storages.map((storage) => ({ id: storage.id, label: `${character.character} - ${storage.storage}` })),
  ),
)
/** In the valuable-only view a tier without valuable mods has nothing to show. */
const tiers = computed(() => (settings.view === 'valuable' ? (summary.value?.tiers ?? []).filter((tier) => tier.valuableCount > 0) : (summary.value?.tiers ?? [])))

/** The value shown in the input: per module, or per 1-star mod (3 modules); the stored value is always per module. */
const rateInput = computed({
  get: () => round(settings.method === 'perOneStar' ? settings.leadersPerModule * 3 : settings.leadersPerModule),
  set: (value: number) => {
    const entered = Number.isFinite(value) ? Math.max(0, value) : 0
    settings.leadersPerModule = settings.method === 'perOneStar' ? entered / 3 : entered
  },
})

function round(value: number): number {
  return Math.round(value * 1000) / 1000
}

function leaders(value: number): string {
  return value.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 })
}

function visibleRows(tier: StarTier): ModuleRow[] {
  if (settings.view === 'all') return tier.rows
  return tier.rows.filter((row) => row.valuable === (settings.view === 'valuable'))
}

function isCollapsed(tier: StarTier): boolean {
  return settings.collapsed.includes(tier.stars)
}

function toggleCollapsed(tier: StarTier): void {
  settings.collapsed = isCollapsed(tier) ? settings.collapsed.filter((stars) => stars !== tier.stars) : [...settings.collapsed, tier.stars]
}

/** Flips one mod at one star tier between junk and valuable; the list is recalculated by the watcher. */
function toggleRow(row: ModuleRow): void {
  const tiers = summary.value?.tiers.map((tier) => tier.stars) ?? []
  settings.valuable = toggleValuable(settings.valuable, row.name, row.stars, row.valuable, tiers)
}

function preset(name: keyof typeof PRESETS): void {
  settings.valuable = PRESETS[name].map((mod) => ({ ...mod }))
}

let timer: ReturnType<typeof setTimeout> | undefined
let latest = 0

async function load(): Promise<void> {
  const ticket = ++latest
  try {
    const result = await getModuleSummary({
      group: group.value,
      valuable: settings.valuable,
      leadersPerModule: settings.leadersPerModule,
    })
    if (ticket !== latest) return
    summary.value = result
    error.value = null
  } catch (failure) {
    if (ticket === latest) error.value = failure instanceof Error ? failure.message : 'The calculation failed.'
  }
}

watch(
  [group, () => props.dataVersion, () => settings.valuable, () => settings.leadersPerModule],
  () => {
    clearTimeout(timer)
    timer = setTimeout(() => void load(), 250)
  },
  { immediate: true, deep: true },
)

const stars = (count: number) => '★'.repeat(count)

/** Plain-text copy of the junk rows in the layout used for trade posts: mod, stars, count, modules, leaders. */
function asText(data: ModuleSummary): string {
  const lines: string[] = []
  for (const tier of data.tiers) {
    if (tier.count === 0) continue
    for (const row of tier.rows.filter((entry) => !entry.valuable)) {
      lines.push([row.name, stars(row.stars), row.count, row.modules, leaders(row.leaders)].join('\t'))
    }
    lines.push([`${tier.stars}★ subtotal`, '', tier.count, tier.modules, leaders(tier.leaders)].join('\t'))
  }
  lines.push(['GRAND TOTAL', '', `${data.count} mods`, `${data.modules} modules`, `${leaders(data.leaders)} L`].join('\t'))
  return lines.join('\n')
}

async function copy(): Promise<void> {
  if (!summary.value) return
  try {
    await navigator.clipboard.writeText(asText(summary.value))
    copied.value = true
    setTimeout(() => (copied.value = false), 2000)
  } catch {
    error.value = 'The clipboard is not available in this browser.'
  }
}
</script>

<template>
  <main class="modules">
    <p v-if="groupsError || error" class="banner banner-danger" role="alert">{{ groupsError ?? error }}</p>

    <section class="panel settings" aria-label="Calculator settings">
      <div class="row">
        <label>
          Storage
          <select v-model="group">
            <option :value="null">Everything (all characters and storages)</option>
            <option v-for="entry in groups" :key="entry.id" :value="entry.id">{{ entry.label }}</option>
          </select>
        </label>
        <label>
          Price in Leaders (L) per
          <select v-model="settings.method">
            <option value="perModule">legendary module</option>
            <option value="perOneStar">1★ mod (3 modules)</option>
          </select>
        </label>
        <label>
          L per {{ settings.method === "perOneStar" ? "1★ mod" : "module" }}
          <input v-model.number="rateInput" type="number" min="0" step="0.05" />
        </label>
      </div>
      <p class="muted">
        A mod of n stars is worth 3 × 2<sup>n−1</sup> legendary modules (1★ 3, 2★ 6, 3★ 12, 4★ 24). The price is in Leaders (L); enter it per
        module or per 1★ mod, both set the same rate. Everything is saved in this browser.
      </p>
      <div class="row">
        <label>
          Show
          <select v-model="settings.view">
            <option value="junk">Junk mods only (counted)</option>
            <option value="all">All mods (valuable marked ◆, not counted)</option>
            <option value="valuable">Valuable mods only</option>
          </select>
        </label>
        <span class="muted">Valuable: {{ settings.valuable.length }} entries. Start from</span>
        <button type="button" class="btn" @click="preset('standard')">Default list</button>
        <button type="button" class="btn" @click="preset('prime')">Prime effects only</button>
        <button type="button" class="btn" @click="preset('none')">Nothing valuable</button>
        <button type="button" class="btn" @click="reset">Reset everything</button>
      </div>
      <p class="muted">
        Change a mod between junk and valuable with the button at the end of its row (choose "All mods" or "Valuable mods only" to find the
        valuable ones again). The change applies to that star tier only, so 2★ Luck and 3★ Luck are separate.
      </p>
    </section>

    <template v-if="summary">
      <p v-if="summary.tiers.length === 0" class="panel muted">No legendary mods found in the selected storage.</p>
      <template v-else>
        <section class="panel totals" aria-label="Totals">
          <strong v-for="tier in summary.tiers" :key="tier.stars">{{ tier.stars }}★: {{ formatNumber(tier.count) }} mods</strong>
          <strong>Total: {{ formatNumber(summary.count) }} mods</strong>
          <strong>{{ formatNumber(summary.modules) }} modules</strong>
          <strong>{{ leaders(summary.leaders) }} L</strong>
          <span v-if="summary.valuableCount" class="muted">+ {{ formatNumber(summary.valuableCount) }} valuable not counted</span>
          <button type="button" class="btn" @click="copy">{{ copied ? 'Copied' : 'Copy as text' }}</button>
        </section>

        <div class="scroll">
        <table class="grid">
          <thead>
            <tr>
              <th>Mod</th>
              <th>Stars</th>
              <th class="num">Count</th>
              <th class="num">Modules each</th>
              <th class="num">Total modules</th>
              <th class="num">Price (L)</th>
              <th><span class="visually-hidden">Status</span></th>
            </tr>
          </thead>
          <tbody v-for="tier in tiers" :key="tier.stars">
            <tr class="tier" @click="toggleCollapsed(tier)">
              <td>
                <button type="button" class="fold" :aria-expanded="!isCollapsed(tier)" @click.stop="toggleCollapsed(tier)">
                  {{ isCollapsed(tier) ? '▸' : '▾' }} {{ tier.stars }}★ mods
                </button>
              </td>
              <td></td>
              <td class="num">{{ formatNumber(tier.count) }}</td>
              <td class="num">{{ tier.modulesEach }}</td>
              <td class="num">{{ formatNumber(tier.modules) }}</td>
              <td class="num">{{ leaders(tier.leaders) }}</td>
              <td class="muted">{{ tier.valuableCount ? `+${formatNumber(tier.valuableCount)} valuable` : '' }}</td>
            </tr>
            <template v-if="!isCollapsed(tier)">
              <tr v-for="row in visibleRows(tier)" :key="row.name" :class="{ valuable: row.valuable }">
                <td>
                  <span v-if="row.valuable" class="gem" title="Valuable: not counted">◆</span>
                  {{ row.name }}
                </td>
                <td>{{ stars(row.stars) }}</td>
                <td class="num">{{ formatNumber(row.count) }}</td>
                <td class="num">{{ row.modulesEach }}</td>
                <td class="num">{{ row.valuable ? '-' : formatNumber(row.modules) }}</td>
                <td class="num">{{ row.valuable ? '-' : leaders(row.leaders) }}</td>
                <td>
                  <button
                    type="button"
                    class="btn mark"
                    :title="row.valuable ? 'Count this mod as junk again' : 'Mark this mod as valuable (not counted)'"
                    @click="toggleRow(row)"
                  >
                    {{ row.valuable ? 'Make junk' : 'Make valuable' }}
                  </button>
                </td>
              </tr>
            </template>
          </tbody>
          <tfoot>
            <tr class="grand">
              <td>GRAND TOTAL</td>
              <td></td>
              <td class="num">{{ formatNumber(summary.count) }} mods</td>
              <td></td>
              <td class="num">{{ formatNumber(summary.modules) }} modules</td>
              <td class="num">{{ leaders(summary.leaders) }} L</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
        </div>
      </template>
    </template>
  </main>
</template>

<style scoped>
.modules {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px 16px;
}

.settings {
  display: grid;
  gap: 10px;
}

.row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 16px;
  align-items: end;
}

label {
  display: grid;
  gap: 4px;
  font-size: 13px;
}

label.check {
  display: flex;
  align-items: center;
  gap: 6px;
}

.totals {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 20px;
  align-items: center;
}

.scroll {
  overflow-x: auto;
}

.grid {
  border-collapse: collapse;
  width: 100%;
  min-width: 640px;
}

@media (max-width: 700px) {
  .modules {
    padding: 8px 10px;
  }

  .row > label,
  .row > label select,
  .row > label input[type='number'] {
    width: 100%;
  }

  .row > label {
    flex: 1 1 100%;
  }
}

.grid th,
.grid td {
  padding: 4px 10px;
  border-bottom: 1px solid var(--border);
  text-align: left;
}

.num {
  text-align: right !important;
  font-variant-numeric: tabular-nums;
}

.tier td,
.grand td {
  font-weight: 600;
  background: var(--surface);
}

.tier {
  cursor: pointer;
}

.fold {
  padding: 0;
  font: inherit;
  font-weight: 600;
  border: 0;
  background: none;
  cursor: pointer;
}

.valuable td {
  background: var(--accent-soft);
  color: var(--muted, inherit);
}

.gem {
  color: var(--accent);
}

.mark {
  padding: 2px 8px;
  font-size: 12px;
  white-space: nowrap;
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}
</style>
