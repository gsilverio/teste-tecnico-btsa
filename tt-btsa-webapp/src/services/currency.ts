const brlFormatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function formatCurrency(value: number) {
  return brlFormatter.format(value)
}

/** Valida centavos pelo texto e recusa valores que perderiam precisão no JSON. */
export function parseMoney(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (!/^\d+(?:\.\d{1,2})?$/.test(normalized)) return null
  const [whole, fraction = ''] = normalized.split('.')
  const cents = BigInt(whole) * 100n + BigInt(fraction.padEnd(2, '0'))
  if (cents > BigInt(Number.MAX_SAFE_INTEGER)) return null
  const amount = Number(normalized)
  return BigInt(amount.toFixed(2).replace('.', '')) === cents ? amount : null
}
