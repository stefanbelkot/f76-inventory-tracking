/**
 * The web version keeps every visitor's uploads in a session. The session id is a random 32-digit hex number that is
 * part of the page address (?session=...), so the link can be shared and bookmarked, and it is remembered in the
 * browser so that opening the plain address returns to the last session.
 */
export const SESSION_HEADER = 'X-InventoryTracking-Session'
export const SESSION_PARAM = 'session'
const STORAGE_KEY = 'inventorytracking.session'

const SESSION_ID = /^[0-9a-f]{32}$/

let current: string | null = null

/** The session that API calls name in their header; null in local mode. */
export function getSessionId(): string | null {
  return current
}

export function setSessionId(id: string | null): void {
  current = id
}

export function isSessionId(value: string | null | undefined): value is string {
  return typeof value === 'string' && SESSION_ID.test(value)
}

/** The session named in the address, or null when there is none (or it is malformed). */
export function sessionFromUrl(search: string = window.location.search): string | null {
  const value = new URLSearchParams(search).get(SESSION_PARAM)?.trim().toLowerCase()
  return isSessionId(value) ? value : null
}

/** The session this browser used last, or null. Storage can be blocked, so every access is guarded. */
export function rememberedSession(storage: Pick<Storage, 'getItem'> | null = safeStorage()): string | null {
  try {
    const value = storage?.getItem(STORAGE_KEY)
    return isSessionId(value) ? value : null
  } catch {
    return null
  }
}

export function rememberSession(id: string, storage: Pick<Storage, 'setItem'> | null = safeStorage()): void {
  try {
    storage?.setItem(STORAGE_KEY, id)
  } catch {
    // The address still holds the session; only the "resume" convenience is lost.
  }
}

export function forgetSession(storage: Pick<Storage, 'removeItem'> | null = safeStorage()): void {
  try {
    storage?.removeItem(STORAGE_KEY)
  } catch {
    // Nothing to do.
  }
}

/** The page address with the session id set (other parameters and the path are kept). */
export function addressWithSession(id: string, href: string = window.location.href): string {
  const url = new URL(href)
  url.searchParams.set(SESSION_PARAM, id)
  return url.toString()
}

function safeStorage(): Storage | null {
  try {
    return window.localStorage
  } catch {
    return null
  }
}
