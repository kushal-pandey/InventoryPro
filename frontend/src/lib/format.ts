const money = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  maximumFractionDigits: 2,
})
const compactMoney = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  notation: 'compact',
  maximumFractionDigits: 1,
})
const number = new Intl.NumberFormat('en-IN')
const dateOnly = new Intl.DateTimeFormat('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
const dateTime = new Intl.DateTimeFormat('en-IN', {
  day: '2-digit',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})
const shortDate = new Intl.DateTimeFormat('en-IN', { day: '2-digit', month: 'short' })

export const formatMoney = (value: number) => money.format(value)
export const formatCompactMoney = (value: number) => compactMoney.format(value)
export const formatNumber = (value: number) => number.format(value)

// The API sends UTC timestamps without a zone suffix; treat them as UTC explicitly.
const asUtc = (iso: string) => new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(iso) ? iso : `${iso}Z`)

export const formatDate = (iso: string) => dateOnly.format(asUtc(iso))
export const formatDateTime = (iso: string) => dateTime.format(asUtc(iso))
export const formatShortDate = (isoDate: string) => shortDate.format(new Date(`${isoDate}T00:00:00`))

export const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
