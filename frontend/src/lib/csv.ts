type Cell = string | number | null | undefined

// Spreadsheet apps execute cells that start with = + - @ as formulas. Product names come from users,
// so neutralise them (CSV/formula injection) before exporting.
function escapeCell(cell: Cell): string {
  let text = cell === null || cell === undefined ? '' : String(cell)
  if (/^[=+\-@\t\r]/.test(text) && typeof cell !== 'number') text = `'${text}`
  return `"${text.replace(/"/g, '""')}"`
}

export function toCsv(rows: Cell[][]): string {
  return rows.map((row) => row.map(escapeCell).join(',')).join('\r\n')
}

export function downloadCsv(filename: string, rows: Cell[][]): void {
  // BOM so Excel opens UTF-8 (e.g. the rupee sign) correctly.
  const blob = new Blob([`\uFEFF${toCsv(rows)}`], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}
