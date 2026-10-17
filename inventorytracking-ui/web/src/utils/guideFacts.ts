/** What the Guide page says was tested; the only place to change when a new build is published. The downloads table has its own numbers (downloads.json). */
export const GUIDE_FACTS = {
  /** The label the mod draws in the storage menu (IT_VERSION in the mod). */
  modLabel: 'IT v1.4',
  game: '1.7.26.22',
  xscal: '0.2.20',
  inventomatic: '2.85',
  /** Who runs the site and how to reach them; add `email` here when a public address exists (the privacy section shows it when set). */
  owner: 'Stefan Belkot',
  repoUrl: 'https://github.com/stefanbelkot/f76-inventory-tracking',
  email: '',
}

export interface GuideSection {
  id: string
  title: string
}

/** The sections in page order; each needs an element with the id "guide-<id>" in GuidePage.vue (checked by a test). */
export const GUIDE_SECTIONS: GuideSection[] = [
  { id: 'overview', title: 'What this is' },
  { id: 'requirements', title: 'What you need' },
  { id: 'versions', title: 'Which mod version' },
  { id: 'install', title: 'Install the mod' },
  { id: 'ingame', title: 'Use it in the game' },
  { id: 'data', title: 'Get the data into the viewer' },
  { id: 'viewer', title: 'Using the viewer' },
  { id: 'help', title: 'Troubleshooting' },
  { id: 'downloads', title: 'Downloads' },
  { id: 'privacy', title: 'About, contact and privacy' },
]

export const SECTION_PREFIX = 'guide'

export function sectionAnchor(id: string): string {
  return `${SECTION_PREFIX}-${id}`
}

/** "#guide" or "#guide-install": whether the address asks for the Guide page, and which section. */
export function parseGuideHash(hash: string): { open: boolean; section: string | null } {
  const value = hash.replace(/^#/, '')
  if (value === SECTION_PREFIX) return { open: true, section: null }
  const id = value.startsWith(`${SECTION_PREFIX}-`) ? value.slice(SECTION_PREFIX.length + 1) : null
  return id && GUIDE_SECTIONS.some((section) => section.id === id) ? { open: true, section: id } : { open: false, section: null }
}
