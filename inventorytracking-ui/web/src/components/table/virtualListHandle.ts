/** The methods VirtualList exposes to its parent through a template ref. */
export interface VirtualListHandle {
  scrollToIndex(index: number): void
  /** How many rows fit on one page of the viewport. */
  pageSize(): number
}
