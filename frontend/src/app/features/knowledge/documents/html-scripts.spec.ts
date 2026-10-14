import { describe, expect, it } from 'vitest';
import { HtmlScriptGate } from './html-script-gate';
import { hasScripts, richKind } from './html-scripts';

describe('hasScripts', () => {
  it('passes plain HTML', () => {
    expect(hasScripts('<h1>Hi</h1><a href="https://x.test">x</a><p>script one</p>')).toBe(false);
  });
  it('detects script tags, inline handlers and javascript urls', () => {
    expect(hasScripts('<SCRIPT>alert(1)</SCRIPT>')).toBe(true);
    expect(hasScripts('<script src="a.js"></script>')).toBe(true);
    expect(hasScripts('<img src=x onerror="alert(1)">')).toBe(true);
    expect(hasScripts('<a href="javascript:alert(1)">x</a>')).toBe(true);
    expect(hasScripts("<a href=' JavaScript:void(0)'>x</a>")).toBe(true);
  });
});

describe('richKind', () => {
  it('classifies by type or extension', () => {
    expect(richKind('a.md', 'text/plain')).toBe('markdown');
    expect(richKind('a', 'text/markdown; charset=utf-8')).toBe('markdown');
    expect(richKind('a.HTM', 'application/octet-stream')).toBe('html');
    expect(richKind('a.txt', 'text/plain')).toBe('other');
  });
});

describe('HtmlScriptGate', () => {
  it('does not gate html without scripts', () => {
    const gate = new HtmlScriptGate();
    gate.inspect('<p>hi</p>');
    expect(gate.gated()).toBe(false);
    expect(gate.running()).toBe(false);
  });
  it('gates scripts until the user runs them', () => {
    const gate = new HtmlScriptGate();
    gate.inspect('<script>1</script>');
    expect(gate.gated()).toBe(true);
    expect(gate.running()).toBe(false);
    gate.run();
    expect(gate.gated()).toBe(false);
    expect(gate.running()).toBe(true);
  });
  it('ignores run without scripts and resets on target change', () => {
    const gate = new HtmlScriptGate();
    gate.inspect('<p>x</p>');
    gate.run();
    expect(gate.running()).toBe(false);
    gate.inspect('<script></script>');
    gate.run();
    gate.reset();
    expect(gate.scripts()).toBeNull();
    expect(gate.running()).toBe(false);
    gate.inspect('<script></script>');
    expect(gate.gated()).toBe(true);
  });
});
