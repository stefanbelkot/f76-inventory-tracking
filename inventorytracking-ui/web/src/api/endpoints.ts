import type {
  Capabilities,
  DirectoryListing,
  FolderResponse,
  GameCandidate,
  GroupsResponse,
  ItemDetail,
  ItemLinks,
  ItemsResponse,
  ModuleSummary,
  Preferences,
  SessionResponse,
  StatusResponse,
  DeleteFilesResponse,
  UploadResponse,
} from '../types/api'
import { getBlob, getJson, sendForm, sendJson } from './client'
import { toQueryString, type ItemFilters, type Paging } from './query'

export const getCapabilities = () => getJson<Capabilities>('/capabilities')

export const getStatus = () => getJson<StatusResponse>('/status')

export const refreshData = () => sendJson<{ dataVersion: number }>('POST', '/refresh')

export const getGroups = () => getJson<GroupsResponse>('/groups')

export const getItems = (filters: ItemFilters, paging: Paging) =>
  getJson<ItemsResponse>(`/items?${toQueryString(filters, paging)}`)

export const getItem = (key: string) => getJson<ItemDetail>(`/items/${encodeURIComponent(key)}`)

/** The default links of an item, or links for edited search terms when `terms` is given. */
export function getLinks(itemKey: string, terms?: string): Promise<ItemLinks> {
  const params = new URLSearchParams({ item: itemKey })
  if (terms !== undefined) params.set('q', terms)
  return getJson<ItemLinks>(`/links?${params.toString()}`)
}

export const getCandidates = () => getJson<{ candidates: GameCandidate[] }>('/setup/candidates')

export const listDirs = (path: string) => getJson<DirectoryListing>(`/setup/dirs?path=${encodeURIComponent(path)}`)

export const resolveFolder = (path: string, confirm: boolean) =>
  sendJson<FolderResponse>('POST', '/setup/folder', { path, confirm })

export const removeFolder = (path: string) => sendJson<{ folders: string[] }>('POST', '/setup/folder/remove', { path })

export const createSession = () => sendJson<SessionResponse>('POST', '/sessions')

export const getSession = () => getJson<SessionResponse>('/session')

/** Uploads one or more export files into the current session; the answer has one result per file. */
export function uploadFiles(files: File[]): Promise<UploadResponse> {
  const form = new FormData()
  for (const file of files) form.append('files', file, file.name)
  return sendForm<UploadResponse>('/session/files', form)
}

export const deleteSessionFiles = (names: string[]) =>
  sendJson<DeleteFilesResponse>('POST', '/session/files/delete', { names })

export const deleteSession = () => sendJson<{ deleted: boolean }>('POST', '/session/delete')

export const getPreferences = () => getJson<Preferences>('/preferences')

export const savePreferences = (preferences: Preferences) => sendJson<Preferences>('PUT', '/preferences', preferences)

export const shutdownViewer = () => sendJson<{ stopping: boolean }>('POST', '/shutdown')

/** The filtered view as a file; the page saves it itself because downloads need the custom header. */
export const exportView = (format: 'csv' | 'json', filters: ItemFilters) =>
  getBlob(`/export?format=${format}&${toQueryString(filters)}`)

export interface ModuleSummaryRequest {
  group: string | null
  valuable: { name: string; stars: number | null }[]
  leadersPerModule: number
}

export const getModuleSummary = (request: ModuleSummaryRequest) =>
  sendJson<ModuleSummary>('POST', '/modules/summary', request)
