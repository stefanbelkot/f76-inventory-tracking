/** A path as a comparison key: Windows paths ignore case and a trailing separator. */
export function folderKey(path: string): string {
  return path.replace(/[\\/]+$/, '').toLowerCase()
}

export function isSameFolder(left: string, right: string): boolean {
  return folderKey(left) === folderKey(right)
}
