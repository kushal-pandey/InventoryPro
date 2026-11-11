import { describe, expectTypeOf, it } from 'vitest'
import { productsApi } from './endpoints'
import type { PagedResult, Product } from './types'

describe('productsApi', () => {
  it('exposes low-stock results as a paged product response', () => {
    expectTypeOf<Awaited<ReturnType<typeof productsApi.lowStock>>>()
      .toEqualTypeOf<PagedResult<Product>>()
  })
})
