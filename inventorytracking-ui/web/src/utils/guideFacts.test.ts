import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { GUIDE_SECTIONS, parseGuideHash, sectionAnchor } from './guideFacts'

describe('guide sections', () => {
  const page = readFileSync(new URL('../pages/GuidePage.vue', import.meta.url), 'utf8')

  it('has unique ids and an element for every section in the page', () => {
    expect(new Set(GUIDE_SECTIONS.map((section) => section.id)).size).toBe(GUIDE_SECTIONS.length)
    for (const section of GUIDE_SECTIONS) {
      expect(page).toContain(`:id="sectionAnchor('${section.id}')"`)
    }
  })

  it('states what was tested: xScal yes, ZFE not tested', () => {
    expect(page).toContain('Not tested with ZFE')
    expect(page).toContain('xScal {{ facts.xscal }}')
  })
})

describe('parseGuideHash', () => {
  it('opens on #guide and on a known section, not on anything else', () => {
    expect(parseGuideHash('#guide')).toEqual({ open: true, section: null })
    expect(parseGuideHash(`#${sectionAnchor('install')}`)).toEqual({ open: true, section: 'install' })
    expect(parseGuideHash('#guide-nope')).toEqual({ open: false, section: null })
    expect(parseGuideHash('')).toEqual({ open: false, section: null })
  })
})