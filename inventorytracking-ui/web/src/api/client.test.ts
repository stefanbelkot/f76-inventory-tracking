import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, REQUIRED_HEADER, getJson, sendForm, sendJson } from './client'
import { SESSION_HEADER, setSessionId } from './session'

function mockFetch(response: Response | Error) {
  const fetchMock = vi.fn(async () => {
    if (response instanceof Error) throw response
    return response
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => vi.unstubAllGlobals())

describe('api client', () => {
  it('sends the required custom header and the /api/v1 prefix on every call', async () => {
    const fetchMock = mockFetch(Response.json({ ok: true }))

    await getJson('/status')

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('/api/v1/status')
    expect(new Headers(init.headers).get(REQUIRED_HEADER)).toBe('1')
  })

  it('names the current session in a header, and only when there is one', async () => {
    const fetchMock = vi.fn(async () => Response.json({}))
    vi.stubGlobal('fetch', fetchMock)

    await getJson('/status')
    setSessionId('0123456789abcdef0123456789abcdef')
    await getJson('/status')
    setSessionId(null)

    const headers = (call: number) => new Headers((fetchMock.mock.calls[call] as unknown as [string, RequestInit])[1].headers)
    expect(headers(0).get(SESSION_HEADER)).toBeNull()
    expect(headers(1).get(SESSION_HEADER)).toBe('0123456789abcdef0123456789abcdef')
  })

  it('uploads form data without forcing a content type', async () => {
    const fetchMock = mockFetch(Response.json({ results: [] }))
    const form = new FormData()
    form.append('files', new Blob(['x']), 'itemdump_a_b_stash_1.json')

    await sendForm('/session/files', form)

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('/api/v1/session/files')
    expect(init.method).toBe('POST')
    expect(init.body).toBe(form)
    expect(new Headers(init.headers).get('Content-Type')).toBeNull()
  })

  it('sends JSON bodies with a content type', async () => {
    const fetchMock = mockFetch(Response.json({ saved: true }))

    await sendJson('POST', '/setup/folder', { path: 'C:\\x', confirm: false })

    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(init.method).toBe('POST')
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json')
    expect(init.body).toBe('{"path":"C:\\\\x","confirm":false}')
  })

  it('turns an error response into an ApiError with the server message', async () => {
    mockFetch(Response.json({ error: 'No modsdata folder was found.' }, { status: 400 }))

    await expect(getJson('/x')).rejects.toMatchObject({ message: 'No modsdata folder was found.', status: 400 })
  })

  it('falls back to a status message when the error body is not JSON', async () => {
    mockFetch(new Response('Forbidden', { status: 403 }))

    await expect(getJson('/x')).rejects.toThrow('The request failed (403).')
  })

  it('reports an unreachable server as status 0', async () => {
    mockFetch(new TypeError('network'))

    const error = await getJson('/x').catch((failure: unknown) => failure)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(0)
  })
})
