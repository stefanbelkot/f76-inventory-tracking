/** The filter state of the item list; it maps one-to-one to the query string of /items and /export. */
export interface ItemFilters {
  group: string | null
  /** Every storage of one character (needs the account too); ignored when a group is set. */
  account: string | null
  character: string | null
  types: string[]
  text: string
  legendary: boolean
  favorite: boolean
  equipped: boolean
  quest: boolean
  vendor: boolean
  stars: number[]
  sort: string
}

export const DEFAULT_SORT = 'name'

export function emptyFilters(): ItemFilters {
  return {
    group: null,
    account: null,
    character: null,
    types: [],
    text: '',
    legendary: false,
    favorite: false,
    equipped: false,
    quest: false,
    vendor: false,
    stars: [],
    sort: DEFAULT_SORT,
  }
}

export interface Paging {
  page: number
  pageSize: number
}

/** Builds the query string (without the leading "?"). Switched-off filters are left out. */
export function toQueryString(filters: ItemFilters, paging?: Paging): string {
  const params = new URLSearchParams()
  if (filters.group) params.set('group', filters.group)
  else if (filters.character !== null) {
    params.set('character', filters.character)
    params.set('account', filters.account ?? '')
  }
  if (filters.types.length > 0) params.set('type', filters.types.join(','))
  if (filters.text.trim()) params.set('q', filters.text.trim())
  if (filters.legendary) params.set('legendary', 'true')
  if (filters.favorite) params.set('favorite', 'true')
  if (filters.equipped) params.set('equipped', 'true')
  if (filters.quest) params.set('quest', 'true')
  if (filters.vendor) params.set('vendor', 'true')
  if (filters.stars.length > 0) params.set('stars', filters.stars.join(','))
  if (filters.sort) params.set('sort', filters.sort)
  if (paging) {
    params.set('page', String(paging.page))
    params.set('pageSize', String(paging.pageSize))
  }
  return params.toString()
}

/** Sort text for a column: "name" ascending, "-name" descending. */
export function toggleSort(current: string, field: string): string {
  return current === field ? `-${field}` : field
}

export function sortField(sort: string): string {
  return sort.startsWith('-') ? sort.slice(1) : sort
}

export function isDescending(sort: string): boolean {
  return sort.startsWith('-')
}
