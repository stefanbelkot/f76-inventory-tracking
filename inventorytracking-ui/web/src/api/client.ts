import { SESSION_HEADER, getSessionId } from './session'

const API_ROOT = '/api/v1'

/** Every API call must carry this header; the server rejects calls without it (spec 8.5). */
export const REQUIRED_HEADER = 'X-InventoryTracking'

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

async function send(path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  headers.set(REQUIRED_HEADER, '1')
  const session = getSessionId()
  if (session) headers.set(SESSION_HEADER, session)
  let response: Response
  try {
    response = await fetch(API_ROOT + path, { ...init, headers })
  } catch {
    throw new ApiError('The viewer cannot be reached.', 0)
  }
  if (!response.ok) {
    throw new ApiError(await errorMessage(response), response.status)
  }
  return response
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { error?: string }
    if (body.error) return body.error
  } catch {
    // The body is not JSON; fall through to the status text.
  }
  return `The request failed (${response.status}).`
}

function jsonBody(method: string, body: unknown): RequestInit {
  return { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }
}

export async function getJson<T>(path: string): Promise<T> {
  return (await send(path)).json() as Promise<T>
}

export async function sendJson<T>(method: 'POST' | 'PUT', path: string, body?: unknown): Promise<T> {
  const init = body === undefined ? { method } : jsonBody(method, body)
  return (await send(path, init)).json() as Promise<T>
}

/** Posts multipart form data (file uploads); the browser sets the content type with its boundary. */
export async function sendForm<T>(path: string, form: FormData): Promise<T> {
  return (await send(path, { method: 'POST', body: form })).json() as Promise<T>
}

export async function getBlob(path: string): Promise<Blob> {
  return (await send(path)).blob()
}
