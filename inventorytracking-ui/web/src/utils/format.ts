import type { ItemGroup } from '../types/api'

const STORAGE_LABELS: Record<string, string> = {
  player: 'Inventory',
  stash: 'Stash box',
  scrap: 'Scrap box',
  ammo: 'Ammo box',
  aid: 'Aid box',
}

export function storageLabel(storage: string): string {
  return STORAGE_LABELS[storage] ?? storage
}

export function formatNumber(value: number): string {
  return value.toLocaleString('en-US')
}

export function formatWeight(value: number): string {
  return value.toLocaleString('en-US', { maximumFractionDigits: 2 })
}

/** A short relative time such as "5 min ago"; the age of an export tells how stale a capture is. */
export function formatAge(isoTime: string, now: Date = new Date()): string {
  const seconds = Math.max(0, Math.round((now.getTime() - new Date(isoTime).getTime()) / 1000))
  if (seconds < 60) return 'just now'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes} min ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 48) return `${hours} h ago`
  return `${Math.floor(hours / 24)} days ago`
}

export function formatDateTime(isoTime: string): string {
  return new Date(isoTime).toLocaleString('en-GB')
}

export function statusText(group: ItemGroup): string {
  switch (group.status) {
    case 'complete':
      return 'Complete'
    case 'incomplete':
      return group.missingParts.length > 0
        ? `Incomplete: missing part ${group.missingParts.join(', ')}`
        : `Incomplete: ${group.rows} of ${group.expectedRows ?? '?'} rows`
    case 'mixedSnapshots':
      return 'Mixed snapshots: parts come from different saves'
  }
}

export function starGlyphs(stars: number): string {
  return stars > 0 ? '★'.repeat(stars) : ''
}
