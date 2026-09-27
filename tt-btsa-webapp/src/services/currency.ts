const brlFormatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function formatCurrency(value: number) {
  return brlFormatter.format(value)
}
