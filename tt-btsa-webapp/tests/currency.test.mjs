import assert from 'node:assert/strict'
import test from 'node:test'
import { parseMoney } from '../src/services/currency.ts'

test('accepts decimal cents, comma input and zero', () => {
  for (const input of ['0', '0.07', '0.29', '1.10', '2.30', '1,10', ' 001.10 ']) {
    assert.equal(parseMoney(input), Number(input.trim().replace(',', '.')))
  }
})

test('rejects invalid precision, negative values and unsafe numbers', () => {
  for (const input of ['', '-1', '100.001', '1e2', 'Infinity', '1.2.3', '90071992547409.92', '9999999999999999.99']) {
    assert.equal(parseMoney(input), null, input)
  }
})
