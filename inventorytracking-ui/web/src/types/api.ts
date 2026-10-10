// Mirrors the JSON of the /api/v1 endpoints (src/InventoryTracking.Server/Api/Contracts and the Core models).

export type GroupStatus = 'complete' | 'incomplete' | 'mixedSnapshots'

export type IssueKind = 'unreadable' | 'invalid' | 'tooLarge' | 'needsNewerViewer' | 'duplicatePart'

export type FolderOrigin = 'saved' | 'commandLine'

export type FolderKind = 'modsData' | 'installFolder' | 'parentFolder' | 'dataFolder' | 'exportFolder'

export interface ItemTypeInfo {
  id: string
  name: string
  bits: number[]
}

export type ViewerMode = 'local' | 'web' | 'both'

/** What an upload session accepts and how long its data is kept (web and both mode). */
export interface ImportLimits {
  maxFileBytes: number
  maxFiles: number
  maxSessionBytes: number
  sessionDays: number
  memoryMinutes: number
}

export interface Capabilities {
  mode: ViewerMode
  folderSetup: boolean
  folderBrowsing: boolean
  folderWatching: boolean
  shutdown: boolean
  version: string
  itemTypes: ItemTypeInfo[]
  import: boolean
  importLimits: ImportLimits | null
}

export interface SessionFile {
  name: string
  size: number
  modifiedUtc: string
}

export interface SessionResponse {
  id: string
  files: SessionFile[]
}

export interface UploadResult {
  name: string
  accepted: boolean
  error: string | null
}

export interface UploadResponse {
  results: UploadResult[]
  files: SessionFile[]
}

export interface DeleteFilesResponse {
  deleted: number
  files: SessionFile[]
}

export interface LoadIssue {
  file: string
  kind: IssueKind
  message: string
}

export interface FolderStatus {
  path: string
  exists: boolean
  exportFiles: number
}

export interface StatusResponse {
  dataVersion: number
  folders: FolderStatus[]
  folderOrigin: FolderOrigin | null
  needsSetup: boolean
  fileCount: number
  loadedAtUtc: string
  issues: LoadIssue[]
}

export interface ItemTotals {
  rows: number
  totalCount: number
  totalWeight: number
  legendaryCount: number
  totalValue: number
}

export interface Currency {
  type: number
  name: string
  amount: number
  max: number | null
}

export interface ItemGroup {
  id: string
  account: string
  character: string
  storage: string
  snapshot: string
  title: string | null
  status: GroupStatus
  rows: number
  expectedRows: number | null
  partsFound: number[]
  partsExpected: number
  missingParts: number[]
  files: string[]
  folders: string[]
  currencies: Currency[]
  capturedAtUtc: string
  totals: ItemTotals
}

export interface CharacterEntry {
  account: string
  character: string
  storages: ItemGroup[]
}

export interface GroupsResponse {
  dataVersion: number
  characters: CharacterEntry[]
}

export interface Effect {
  star: number | null
  text: string
  marker: string | null
}

export interface ItemSummary {
  key: string
  groupId: string
  character: string
  storage: string
  name: string
  count: number
  id: number
  value: number
  totalValue: number
  equipState: number
  equipped: boolean
  filter: number
  types: string[]
  level: number
  health: number
  maxHealth: number
  weight: number
  totalWeight: number
  legendary: boolean
  stars: number
  favorite: boolean
  quest: boolean
  card: string | null
  legendaryMods: number | null
  /** Listed at the player's vending machine; price is the listed price per item. */
  vendor: boolean
  price: number | null
  machine: number | null
}

export interface ItemDetail extends ItemSummary {
  /** Present when the item is assigned to a CAMP machine (machine 1 is the player vending machine). */
  vending?: { machine: number; price: number | null; onOtherMachine: boolean }
  category: number
  desc: string | null
  effects: Effect[]
}

export interface ItemsResponse {
  items: ItemSummary[]
  page: number
  pageSize: number
  totalRows: number
  totals: ItemTotals
}

export interface SearchLinks {
  terms: string
  googleUrl: string
  wikiUrl: string
}

export interface EffectLink {
  star: number
  text: string
  googleUrl: string
}

export interface ItemLinks {
  name: SearchLinks
  effects: EffectLink[]
}

export interface GameCandidate {
  path: string
  label: string
  exportFiles: number
}

export interface DirectoryEntry {
  name: string
  path: string
  hasExports: boolean
}

export interface DirectoryListing {
  path: string
  parent: string | null
  dirs: DirectoryEntry[]
}

export interface FolderResponse {
  path: string
  kind: FolderKind
  exportFiles: number
  saved: boolean
}

export interface Preferences {
  theme: ThemeChoice
  columns: string[] | null
}

export type ThemeChoice = 'system' | 'light' | 'dark'

export interface ModuleRow {
  name: string
  stars: number
  count: number
  modulesEach: number
  modules: number
  leaders: number
  valuable: boolean
}

export interface StarTier {
  stars: number
  count: number
  modulesEach: number
  modules: number
  leaders: number
  valuableCount: number
  rows: ModuleRow[]
}

export interface ModuleSummary {
  tiers: StarTier[]
  count: number
  modules: number
  leaders: number
  valuableCount: number
}
