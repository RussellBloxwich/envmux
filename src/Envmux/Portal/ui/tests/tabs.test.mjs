import assert from 'node:assert/strict'
import test from 'node:test'
import { restoreShellTabs, saveShellTabs } from '../src/tabs.ts'

function storage() {
  const values = new Map()
  return {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  }
}

const first = { id: 'shell:one', kind: 'shell', label: 'shell 1' }
const second = { id: 'shell:two', kind: 'shell', tool: 'codex', label: 'codex 2' }

test('refresh preserves independent terminal identities and tool selection', () => {
  const area = storage()
  saveShellTabs(area, 'project:session', [first, second])
  assert.deepEqual(restoreShellTabs(area, 'project:session', 'reload'), [first, second])
  assert.deepEqual(restoreShellTabs(area, 'another:session', 'reload'), [])
  assert.deepEqual(restoreShellTabs(area, 'project:session', 'navigate'), [])
  saveShellTabs(area, 'project:session', [second])
  assert.deepEqual(restoreShellTabs(area, 'project:session', 'reload'), [second])
})

test('malformed and duplicate saved tabs cannot alias unrelated tabs', () => {
  const area = storage()
  area.setItem('tabs', JSON.stringify([first, first, { ...second, id: 'log' }, null]))
  assert.deepEqual(restoreShellTabs(area, 'tabs', 'reload'), [first])
  area.setItem('tabs', '{')
  assert.deepEqual(restoreShellTabs(area, 'tabs', 'reload'), [])
})

test('unavailable storage leaves in-page terminals usable', () => {
  const area = {
    getItem() { throw new Error('storage denied') },
    setItem() { throw new Error('storage denied') },
  }
  assert.deepEqual(restoreShellTabs(area, 'tabs', 'reload'), [])
  assert.doesNotThrow(() => saveShellTabs(area, 'tabs', [first]))
})
