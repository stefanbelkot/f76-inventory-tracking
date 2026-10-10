import { ref } from 'vue'
import { createSession, getSession } from '../api/endpoints'
import { ApiError } from '../api/client'
import {
  addressWithSession,
  forgetSession,
  isSessionId,
  rememberSession,
  rememberedSession,
  sessionFromUrl,
  setSessionId,
} from '../api/session'

const sessionId = ref<string | null>(null)
/** A one-time message for the user, for example that the session in the address has expired. */
const notice = ref<string | null>(null)

/** Makes a session the current one: remembered in the browser, named in API calls and shown in the address. */
function adopt(id: string): void {
  sessionId.value = id
  setSessionId(id)
  rememberSession(id)
  window.history.replaceState(window.history.state, '', addressWithSession(id))
}

/**
 * The upload session of the web version. At start the session from the address is used, else the one this browser
 * used last, else a new one is created. A session that no longer exists is replaced by a new one with a notice.
 */
export function useSession() {
  async function create(): Promise<void> {
    adopt((await createSession()).id)
  }

  async function init(): Promise<void> {
    const fromUrl = sessionFromUrl()
    const candidate = fromUrl ?? rememberedSession()
    if (isSessionId(candidate)) {
      setSessionId(candidate)
      try {
        await getSession()
        adopt(candidate)
        return
      } catch (failure) {
        setSessionId(null)
        if (!(failure instanceof ApiError) || failure.status !== 404) throw failure
        forgetSession()
        notice.value = fromUrl
          ? 'The session in the address does not exist any more (sessions are deleted after a week without use). A new session was started.'
          : null
      }
    }
    await create()
  }

  /** Starts an empty session next to the old one (the old one stays reachable by its link until it expires). */
  async function startNew(): Promise<void> {
    notice.value = null
    await create()
  }

  /** The address of the current session, for sharing. */
  function shareAddress(): string {
    return sessionId.value ? addressWithSession(sessionId.value) : window.location.href
  }

  return { sessionId, notice, init, startNew, shareAddress }
}
