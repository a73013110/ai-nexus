import { motionDuration, motionTiming } from './motion-tokens';

describe('motion tokens', () => {
  function element(tokens: Record<string, string>) {
    const node = document.createElement('div');
    for (const [name, value] of Object.entries(tokens)) node.style.setProperty(name, value);
    document.body.append(node);
    return node;
  }

  afterEach(() => document.body.replaceChildren());

  it('reads milliseconds as written in source', () => {
    expect(motionDuration(element({ '--motion-panel': '240ms' }), '--motion-panel')).toBe(240);
  });

  it('converts the seconds a production CSS minifier writes', () => {
    expect(motionDuration(element({ '--motion-panel': '.24s' }), '--motion-panel')).toBe(240);
    expect(motionDuration(element({ '--motion-spin': '1s' }), '--motion-spin')).toBe(1000);
  });

  it('settles instantly when the token is missing', () => {
    expect(motionDuration(element({}), '--motion-panel')).toBe(0);
  });

  it('pairs the duration with the easing token', () => {
    const node = element({ '--motion-morph': '.46s', '--ease-spring': 'ease-in' });
    expect(motionTiming(node, '--motion-morph', '--ease-spring')).toEqual({
      duration: 460,
      easing: 'ease-in',
    });
    expect(motionTiming(element({}), '--motion-panel').easing).toBe('linear');
  });
});
