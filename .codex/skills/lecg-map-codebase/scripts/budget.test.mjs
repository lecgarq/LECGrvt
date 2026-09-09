import test from 'node:test';
import assert from 'node:assert/strict';
import { measurePacket, emitPacket, utf8Bytes } from './budget.mjs';

const packet = evidence => `FRESHNESS\n  index stale\nORIENTATION\n  LECG, net8.0-windows\nEVIDENCE\n  ${evidence}\nSYMBOLS\n  none resolved\nREAD NEXT\n  none\nUNRESOLVED\n  unavailable\nCOST\n  2/8 tool calls\n`;

test('measures the full UTF-8 packet including headings and line endings', () => {
  const text = packet('[inventory] Materiales — 界');
  const result = measurePacket(text);
  assert.equal(result.ok, true);
  assert.equal(result.bytes, Buffer.byteLength(text));
  assert.equal(result.sections.reduce((total, section) => total + section.bytes, 0), result.bytes);
  assert.equal(measurePacket(text.replaceAll('\n', '\r\n')).ok, true);
});

test('reports exact byte boundary, oversized sections and invalid shape', () => {
  const room = 6144 - Buffer.byteLength(packet(''));
  assert.equal(measurePacket(packet('x'.repeat(room))).ok, true);
  const oversized = measurePacket(packet('x'.repeat(room + 1)));
  assert.equal(oversized.ok, false);
  assert.ok(oversized.errors.some(error => error.includes('by 1 bytes') && error.includes('EVIDENCE')));
  assert.ok(oversized.warnings.some(warning => warning.startsWith('EVIDENCE:')));
  for (const text of ['', 'preamble\n' + packet(''), packet('').replace('SYMBOLS', 'EVIDENCE'),
    packet('').replace('READ NEXT\n', '')]) assert.equal(measurePacket(text).ok, false);
});

test('emitter validates in-process without spending a contingency call', () => {
  const sections = Object.fromEntries(['FRESHNESS','ORIENTATION','EVIDENCE','SYMBOLS','READ NEXT','UNRESOLVED'].map(k => [k, 'none']));
  const ledger = { calls: 8, evidenceBytes: 5000 };
  assert.match(emitPacket(sections, ledger), /8\/8 tool calls/);
  assert.deepEqual(ledger, { calls: 8, evidenceBytes: 5000 });
  assert.throws(() => emitPacket(sections, { calls: 9, evidenceBytes: 10 }));
  assert.throws(() => emitPacket(sections, { calls: 2, evidenceBytes: 6145 }));
  assert.throws(() => emitPacket({ ...sections, EVIDENCE: '界'.repeat(3000) }, ledger));
  assert.equal(utf8Bytes('abc界🙂\ud800'), Buffer.byteLength('abc界🙂\ud800'));
});
