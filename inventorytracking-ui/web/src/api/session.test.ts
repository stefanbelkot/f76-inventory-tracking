import { afterEach, describe, expect, it } from 'vitest'
import {
  addressWithSession,
  getSessionId,
  isSessionId,
  rememberSession,
  rememberedSession,
  sessionFromUrl,
  setSessionId,
} from './session'

const ID = '0123456789abcdef0123456789abcdef'

afterEach(() => setSessionId(null))

function storage(initial: Record<string, string> = {}) {
  const data = { ...initial }
  return {
    getItem: (key: string) => data[key] ?? null,
    setItem: (key: string, value: string) => void (data[key] = value),
    data,
  }
}

describe('session ids', () => {
  it('accepts exactly 32 lowercase hex digits', () => {
    expect(isSessionId(ID)).toBe(true)
    expect(isSessionId(ID.toUpperCase())).toBe(false)
    expect(isSessionId(ID.slice(1))).toBe(false)
    expect(isSessionId('../../etc/passwd')).toBe(false)
    expect(isSessionId(null)).toBe(false)
  })

  it('reads the session from the address, tolerating case and spaces', () => {
    expect(sessionFromUrl(`?session=${ID}`)).toBe(ID)
    expect(sessionFromUrl(`?x=1&session=${ID.toUpperCase()}`)).toBe(ID)
    expect(sessionFromUrl('?session=nope')).toBeNull()
    expect(sessionFromUrl('')).toBeNull()
  })

  it('writes the session into the address and keeps the rest of it', () => {
    expect(addressWithSession(ID, 'https://example.org/viewer/?a=1#top')).toBe(`https://example.org/viewer/?a=1&session=${ID}#top`)
    expect(addressWithSession(ID, `https://example.org/?session=${'f'.repeat(32)}`)).toBe(`https://example.org/?session=${ID}`)
  })
})

describe('remembered session', () => {
  it('round-trips through storage and ignores garbage', () => {
    const store = storage()
    expect(rememberedSession(store)).toBeNull()

    rememberSession(ID, store)

    expect(rememberedSession(store)).toBe(ID)
    expect(rememberedSession(storage({ 'inventorytracking.session': 'broken' }))).toBeNull()
  })

  it('survives storage that throws', () => {
    const blocked = {
      getItem: () => {
        throw new Error('blocked')
      },
      setItem: () => {
        throw new Error('blocked')
      },
    }

    expect(rememberedSession(blocked)).toBeNull()
    expect(() => rememberSession(ID, blocked)).not.toThrow()
  })
})

describe('current session', () => {
  it('is null until one is set', () => {
    expect(getSessionId()).toBeNull()
    setSessionId(ID)
    expect(getSessionId()).toBe(ID)
  })
})
