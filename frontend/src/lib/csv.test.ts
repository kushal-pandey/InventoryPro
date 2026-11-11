import { describe, expect, it } from 'vitest'
import { toCsv } from './csv'

describe('toCsv', () => {
  it('quotes cells and escapes embedded quotes', () => {
    expect(toCsv([['Cable "USB-C"', 3]])).toBe('"Cable ""USB-C""","3"')
  })

  it('neutralises spreadsheet formulas coming from user-entered text', () => {
    expect(toCsv([['=HYPERLINK("http://evil")', '+1', '-2', '@x']])).toBe(
      `"'=HYPERLINK(""http://evil"")","'+1","'-2","'@x"`,
    )
  })

  it('leaves genuine negative numbers alone', () => {
    expect(toCsv([[-5]])).toBe('"-5"')
  })

  it('renders null and undefined as empty cells', () => {
    expect(toCsv([[null, undefined, 'a']])).toBe('"","","a"')
  })
})
