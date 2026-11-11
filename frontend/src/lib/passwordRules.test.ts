import { describe, expect, it } from 'vitest'
import { getPasswordRules } from './passwordRules'

describe('getPasswordRules', () => {
  it('reports the configured Identity password requirements', () => {
    expect(getPasswordRules('short')).toEqual([
      { id: 'length', label: 'At least 12 characters', valid: false },
      { id: 'uppercase', label: 'An uppercase letter', valid: false },
      { id: 'lowercase', label: 'A lowercase letter', valid: true },
      { id: 'number', label: 'A number', valid: false },
      { id: 'symbol', label: 'A symbol', valid: false },
    ])
    expect(getPasswordRules('InventoryPro!2026').every((rule) => rule.valid)).toBe(true)
  })
})
