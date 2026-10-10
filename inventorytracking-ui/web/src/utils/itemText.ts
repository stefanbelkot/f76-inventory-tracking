import type { ItemDetail } from '../types/api'
import { formatNumber, formatWeight, starGlyphs } from './format'

/** The text copied to the clipboard for one item: its name, numbers and every effect line. */
export function itemToText(item: ItemDetail, typeNames: Map<string, string>): string {
  const types = item.types.map((id) => typeNames.get(id) ?? id).join(', ')
  const lines = [
    item.stars > 0 ? `${starGlyphs(item.stars)} ${item.name}` : item.name,
    `Count ${formatNumber(item.count)}, level ${item.level}, weight ${formatWeight(item.weight)}, value ${formatNumber(item.value)}`,
    `Type: ${types}`,
  ]
  for (const effect of item.effects) {
    lines.push(effect.star === null ? `  ${effect.text}` : `  ${starGlyphs(effect.star)} ${effect.text}`)
  }
  return lines.join('\n')
}
