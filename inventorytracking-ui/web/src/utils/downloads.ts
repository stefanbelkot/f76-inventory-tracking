/** One file offered on the Guide page; the list comes from <c>/downloads/downloads.json</c> next to the site. */
export interface DownloadFile {
  id: string
  title: string
  version: string
  /** File name inside the downloads folder. */
  file: string
  bytes: number
  sha256: string
  notes: string
  requires: { game?: string; xscal?: string; inventomatic?: string }
}

export interface DownloadManifest {
  updated: string
  files: DownloadFile[]
}

export const MANIFEST_URL = '/downloads/downloads.json'

function text(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

/** Reads the manifest defensively: entries without a title, file name or a plain file name are dropped. */
export function parseManifest(value: unknown): DownloadManifest | null {
  if (typeof value !== 'object' || value === null) return null
  const raw = value as { updated?: unknown; files?: unknown }
  if (!Array.isArray(raw.files)) return null
  const files: DownloadFile[] = []
  for (const entry of raw.files as Record<string, unknown>[]) {
    const file = text(entry?.file)
    const title = text(entry?.title)
    if (!title || !file || /[\\/]/.test(file) || file.startsWith('.')) continue
    const requires = (typeof entry.requires === 'object' && entry.requires !== null ? entry.requires : {}) as Record<string, unknown>
    files.push({
      id: text(entry.id) || file,
      title,
      version: text(entry.version),
      file,
      bytes: typeof entry.bytes === 'number' && entry.bytes >= 0 ? entry.bytes : 0,
      sha256: /^[0-9a-f]{64}$/i.test(text(entry.sha256)) ? text(entry.sha256).toLowerCase() : '',
      notes: text(entry.notes),
      requires: { game: text(requires.game) || undefined, xscal: text(requires.xscal) || undefined, inventomatic: text(requires.inventomatic) || undefined },
    })
  }
  return { updated: text(raw.updated), files }
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

export function downloadUrl(file: DownloadFile): string {
  return `/downloads/${encodeURIComponent(file.file)}`
}
