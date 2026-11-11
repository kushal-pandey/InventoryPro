import { describe, expect, it } from 'vitest'
import { readOrderListUrl, readProductListUrl, updateListUrl } from './listUrlState'

describe('list URL state', () => {
  it('reads product filters and uses safe defaults for invalid values', () => {
    expect(readProductListUrl(new URLSearchParams('page=3&q=beans&category=12&sort=price%3Adesc&low=true'))).toEqual({
      page: 3, search: 'beans', categoryId: 12, sort: 'price:desc', lowStockOnly: true,
    })
    expect(readProductListUrl(new URLSearchParams('page=-1&category=bad&sort=unknown')).page).toBe(1)
  })

  it('reads order search, status, sort, and page from a shareable URL', () => {
    expect(readOrderListUrl(new URLSearchParams('page=4&q=ord-12&status=4&sort=total%3Aasc'))).toEqual({
      page: 4, search: 'ord-12', status: '4', sort: 'total:asc',
    })
  })

  it('updates filters without losing unrelated parameters and resets pagination when requested', () => {
    const updated = updateListUrl(new URLSearchParams('page=7&tab=open'), { q: 'milk', sort: null }, true)
    expect(updated.get('page')).toBeNull()
    expect(updated.get('tab')).toBe('open')
    expect(updated.get('q')).toBe('milk')
    expect(updated.get('sort')).toBeNull()
  })
})
