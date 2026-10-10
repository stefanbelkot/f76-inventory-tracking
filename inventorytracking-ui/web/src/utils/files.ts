/** Saves a blob through a temporary link; the page does this itself because API calls need the custom header. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** The text of a downloaded blob without a leading byte order mark. */
export async function blobText(blob: Blob): Promise<string> {
  return (await blob.text()).replace(/^﻿/, '')
}
