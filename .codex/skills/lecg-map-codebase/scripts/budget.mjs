const headings = ['FRESHNESS', 'ORIENTATION', 'EVIDENCE', 'SYMBOLS', 'READ NEXT', 'UNRESOLVED', 'COST'];
const maximumBytes = 6 * 1024;
const targets = { ORIENTATION: 1024, EVIDENCE: 3072, SYMBOLS: 2048 };

export function utf8Bytes(text) {
  let bytes = 0;
  for (const char of text) {
    const cp = char.codePointAt(0);
    bytes += cp <= 0x7f ? 1 : cp <= 0x7ff ? 2 : cp <= 0xffff ? 3 : 4;
  }
  return bytes;
}

export function measurePacket(packet) {
  const matches = [...packet.matchAll(/^(FRESHNESS|ORIENTATION|EVIDENCE|SYMBOLS|READ NEXT|UNRESOLVED|COST)\r?$/gm)];
  const totalBytes = utf8Bytes(packet);
  const errors = [];
  if (matches.map(match => match[1]).join('|') !== headings.join('|')
      || packet.slice(0, matches[0]?.index ?? packet.length).trim()) {
    errors.push('Use each required section exactly once, in the fixed order, with no preamble.');
  }
  const sections = matches.map((match, i) => {
    const bytes = utf8Bytes(packet.slice(match.index, matches[i + 1]?.index ?? packet.length));
    return { section: match[1], bytes, ...(targets[match[1]] ? { target_bytes: targets[match[1]] } : {}) };
  });
  const warnings = sections.filter(item => item.target_bytes && item.bytes > item.target_bytes)
    .map(item => `${item.section}: ${item.bytes} bytes exceeds its ${item.target_bytes}-byte planning allowance.`);
  if (totalBytes > maximumBytes) {
    const largest = [...sections].sort((a, b) => b.bytes - a.bytes)[0];
    errors.push(`Packet exceeds 6 KB by ${totalBytes - maximumBytes} bytes; largest section: ${largest?.section ?? 'unstructured text'}.`);
  }
  return { ok: errors.length === 0, bytes: totalBytes, limit_bytes: maximumBytes,
    sections, warnings, errors };
}

// Imported into the emitter's existing runtime during session setup: no I/O and no tool call.
export function emitPacket(sections, ledger) {
  if (!Number.isInteger(ledger.calls) || ledger.calls < 1 || ledger.calls > 8
      || !Number.isInteger(ledger.evidenceBytes) || ledger.evidenceBytes < 0 || ledger.evidenceBytes > maximumBytes)
    throw new Error('Invalid or exceeded query ledger.');
  if (headings.slice(0, -1).some(heading => !Object.hasOwn(sections, heading)))
    throw new Error('Missing packet section.');
  const cost = `${ledger.calls}/8 tool calls | ${(ledger.evidenceBytes / 1024).toFixed(2)} KB evidence`;
  const packet = headings.map(heading => `${heading}\n  ${heading === 'COST' ? cost
    : (Array.isArray(sections[heading]) ? sections[heading].join('\n  ') : sections[heading])}`).join('\n');
  const result = measurePacket(packet);
  if (!result.ok) throw new Error(result.errors.join(' '));
  return packet;
}
