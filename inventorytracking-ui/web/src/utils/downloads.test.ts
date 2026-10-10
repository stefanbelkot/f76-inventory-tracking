import { describe, expect, it } from 'vitest'
import { downloadUrl, formatBytes, parseManifest } from './downloads'

const HASH = 'a'.repeat(64)

describe('parseManifest', () => {
  it('reads files and keeps the requirement versions', () => {
    const manifest = parseManifest({
      updated: '2026-10-10',
      files: [{ id: 'mod', title: 'Mod', version: '1.4', file: 'Mod.zip', bytes: 2048, sha256: HASH.toUpperCase(), notes: 'n', requires: { xscal: '0.2.20', inventomatic: '2.85' } }],
    })
    expect(manifest?.files[0]).toMatchObject({ id: 'mod', bytes: 2048, sha256: HASH, requires: { xscal: '0.2.20', inventomatic: '2.85', game: undefined } })
  })

  it('drops entries without a title or with a path in the file name', () => {
    const manifest = parseManifest({ files: [{ title: 'A', file: '../x.zip' }, { title: '', file: 'b.zip' }, { title: 'C', file: 'c.zip', sha256: 'short' }] })
    expect(manifest?.files.map((file) => file.file)).toEqual(['c.zip'])
    expect(manifest?.files[0]?.sha256).toBe('')
  })

  it('rejects garbage', () => {
    expect(parseManifest(null)).toBeNull()
    expect(parseManifest({ files: 'x' })).toBeNull()
  })
})

describe('formatting', () => {
  it('formats sizes and builds an encoded url', () => {
    expect(formatBytes(512)).toBe('512 B')
    expect(formatBytes(20 * 1024)).toBe('20 KB')
    expect(formatBytes(63 * 1024 * 1024)).toBe('63.0 MB')
    expect(downloadUrl({ file: 'a b.zip' } as never)).toBe('/downloads/a%20b.zip')
  })
})
