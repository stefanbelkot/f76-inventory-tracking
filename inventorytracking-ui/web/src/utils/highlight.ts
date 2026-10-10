export interface TextPart {
  text: string
  match: boolean
}

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

/** The words of a search text, as the server splits them: whitespace separated, case-insensitive. */
export function searchWords(query: string): string[] {
  return query.split(/\s+/).filter((word) => word.length > 0)
}

/** Splits `text` into parts so that every occurrence of a query word can be highlighted. */
export function highlightParts(text: string, query: string): TextPart[] {
  const words = searchWords(query)
  if (words.length === 0 || text.length === 0) return [{ text, match: false }]

  const pattern = new RegExp(words.map(escapeRegExp).sort((a, b) => b.length - a.length).join('|'), 'gi')
  const parts: TextPart[] = []
  let last = 0
  for (const found of text.matchAll(pattern)) {
    const start = found.index ?? 0
    if (start > last) parts.push({ text: text.slice(last, start), match: false })
    parts.push({ text: found[0], match: true })
    last = start + found[0].length
  }
  if (last < text.length) parts.push({ text: text.slice(last), match: false })
  return parts
}
