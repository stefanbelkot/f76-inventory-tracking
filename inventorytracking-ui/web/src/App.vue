<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { refreshData } from './api/endpoints'
import AppHeader from './components/AppHeader.vue'
import IssueList from './components/IssueList.vue'
import { useCapabilities } from './composables/useCapabilities'
import { setBrowserPreferences, usePreferences } from './composables/usePreferences'
import { useSession } from './composables/useSession'
import { useStatus } from './composables/useStatus'
import BrowsePage from './pages/BrowsePage.vue'
import GuidePage from './pages/GuidePage.vue'
import ModulesPage from './pages/ModulesPage.vue'
import ImportPage from './pages/ImportPage.vue'
import SettingsPage from './pages/SettingsPage.vue'
import SetupPage from './pages/SetupPage.vue'
import StoppedPage from './pages/StoppedPage.vue'
import type { View } from './types/view'
import { parseGuideHash } from './utils/guideFacts'

const { capabilities, load: loadCapabilities } = useCapabilities()
const { load: loadPreferences } = usePreferences()
const { sessionId, init: initSession } = useSession()

// Status polling starts once the capabilities are known and, in the web version, the session exists.
const ready = ref(false)
const { status, unreachable, dataVersion, refresh } = useStatus(ready)

const view = ref<View>(parseGuideHash(window.location.hash).open ? 'guide' : 'browse')
const stopped = ref(false)
const refreshing = ref(false)
const sessionProblem = ref<string | null>(null)

const mode = computed(() => capabilities.value?.mode ?? 'local')
const hasSettings = computed(() => capabilities.value?.folderSetup ?? true)
const hasImport = computed(() => capabilities.value?.import ?? false)

/** What the page shows: the upload page when asked for (or when the web version has no files yet), else setup, settings or the lists. */
const page = computed<'import' | 'setup' | 'settings' | 'modules' | 'guide' | 'browse' | null>(() => {
  if (view.value === 'guide') return 'guide'
  if (!status.value) return null
  if (view.value === 'import' && hasImport.value) return 'import'
  if (status.value.needsSetup) return mode.value === 'web' ? 'import' : 'setup'
  if (view.value === 'modules') return 'modules'
  return view.value === 'settings' && hasSettings.value ? 'settings' : 'browse'
})

/** The tab to highlight: the setup page belongs to no tab, so the first one stays active. */
const headerView = computed<View>(() => (page.value === null || page.value === 'setup' ? 'browse' : page.value))

/** The Guide page has an address of its own (#guide, #guide-install); leaving it clears the address again. */
function navigate(next: View): void {
  view.value = next
  if (next === 'guide') {
    if (!parseGuideHash(window.location.hash).open) history.replaceState(null, '', '#guide')
  } else if (parseGuideHash(window.location.hash).open) {
    history.replaceState(null, '', window.location.pathname + window.location.search)
  }
}

async function forceRefresh(): Promise<void> {
  refreshing.value = true
  try {
    await refreshData()
    await refresh()
  } finally {
    refreshing.value = false
  }
}

/** The first folder leaves the setup page; further folders are added from the settings page, which stays open. */
async function folderChanged(): Promise<void> {
  const wasSetup = status.value?.needsSetup ?? false
  await refresh()
  if (wasSetup) view.value = 'browse'
}

/** Files were added or deleted: the server reloaded its data, so the status shows the new data version. */
async function filesChanged(): Promise<void> {
  await refresh()
}

async function sessionChanged(): Promise<void> {
  await refresh()
  view.value = 'import'
}

onMounted(async () => {
  try {
    await loadCapabilities()
    setBrowserPreferences(capabilities.value?.mode === 'web')
    void loadPreferences()
    if (capabilities.value?.import) await initSession()
  } catch (failure) {
    // An unreachable server also shows up in the status polling; a refused session needs its own message.
    sessionProblem.value = failure instanceof Error ? failure.message : 'The session could not be started.'
  } finally {
    ready.value = true
  }
})
</script>

<template>
  <StoppedPage v-if="stopped" />
  <div v-else class="shell">
    <AppHeader
      :view="headerView"
      :show-navigation="status !== null && (!status.needsSetup || hasImport)"
      :refreshing="refreshing"
      :show-settings="hasSettings"
      :show-import="hasImport"
      :show-quit="capabilities?.shutdown ?? false"
      @navigate="navigate"
      @refresh="forceRefresh"
      @stopped="stopped = true"
    />
    <p v-if="sessionProblem" class="banner banner-danger notice" role="alert">{{ sessionProblem }}</p>
    <p v-if="unreachable && !sessionProblem" class="banner banner-danger notice" role="alert">
      The viewer cannot be reached. It may have been stopped or the server is not available; try again in a moment.
    </p>
    <GuidePage v-if="page === 'guide'" />
    <p v-else-if="!status && !unreachable" class="muted notice">Loading...</p>
    <ImportPage v-else-if="page === 'import'" :key="sessionId ?? 'none'" @changed="filesChanged" @session-changed="sessionChanged" />
    <SetupPage v-else-if="page === 'setup'" @saved="folderChanged" />
    <template v-else-if="status">
      <IssueList class="notice" :issues="status.issues" />
      <BrowsePage v-if="page === 'browse'" :key="sessionId ?? 'local'" :data-version="dataVersion" />
      <ModulesPage v-else-if="page === 'modules'" :key="sessionId ?? 'local'" :data-version="dataVersion" />
      <SettingsPage v-else :status="status" @saved="folderChanged" />
    </template>
  </div>
</template>

<style scoped>
.shell {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.shell > :deep(main) {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.notice {
  margin: 8px 16px 0;
}
</style>
