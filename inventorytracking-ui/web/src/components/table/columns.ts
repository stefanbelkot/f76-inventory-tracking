export interface ColumnDef {
  id: string
  label: string
  /** The server sort column, or null when the column cannot be sorted. */
  sort: string | null
  /** Pixel width; the name column takes the remaining space and uses this as its minimum. */
  width: number
  numeric?: boolean
}

export const COLUMNS: ColumnDef[] = [
  { id: 'name', label: 'Name', sort: 'name', width: 220 },
  { id: 'count', label: 'Count', sort: 'count', width: 80, numeric: true },
  { id: 'type', label: 'Type', sort: 'type', width: 130 },
  { id: 'level', label: 'Level', sort: 'level', width: 70, numeric: true },
  { id: 'stars', label: 'Stars', sort: 'stars', width: 80 },
  { id: 'weight', label: 'Weight', sort: 'weight', width: 80, numeric: true },
  { id: 'totalWeight', label: 'Total weight', sort: 'totalWeight', width: 100, numeric: true },
  { id: 'value', label: 'Value', sort: 'value', width: 80, numeric: true },
  { id: 'totalValue', label: 'Total value', sort: 'totalValue', width: 100, numeric: true },
  { id: 'price', label: 'Vendor price', sort: 'price', width: 110, numeric: true },
  { id: 'favorite', label: 'Favorite', sort: 'favorite', width: 80 },
  { id: 'equipped', label: 'Equipped', sort: 'equipped', width: 90 },
  { id: 'character', label: 'Character', sort: 'character', width: 120 },
  { id: 'storage', label: 'Storage', sort: 'storage', width: 110 },
]

export const DEFAULT_COLUMNS = ['name', 'count', 'type', 'level', 'stars', 'weight', 'value', 'price', 'favorite', 'equipped']

/** The "everything" view mixes storages, so it also shows where each row comes from. */
export const EVERYTHING_COLUMNS = ['character', 'storage']

const KNOWN_IDS = new Set(COLUMNS.map((column) => column.id))

/** The columns to show, in table order; unknown ids from an old settings file are dropped. */
export function visibleColumns(saved: string[] | null, everything: boolean): ColumnDef[] {
  const wanted = saved ?? (everything ? [...DEFAULT_COLUMNS, ...EVERYTHING_COLUMNS] : DEFAULT_COLUMNS)
  const valid = new Set(wanted.filter((id) => KNOWN_IDS.has(id)))
  valid.add('name')
  return COLUMNS.filter((column) => valid.has(column.id))
}

export function gridTemplate(columns: ColumnDef[]): string {
  return columns.map((column) => (column.id === 'name' ? `minmax(${column.width}px, 1fr)` : `${column.width}px`)).join(' ')
}

export function tableMinWidth(columns: ColumnDef[]): number {
  return columns.reduce((sum, column) => sum + column.width, 0)
}
