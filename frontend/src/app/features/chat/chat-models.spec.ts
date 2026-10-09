import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NexusApi } from '../../core/api/nexus-api';
import type { Models } from '../../core/api/types';
import { AuthService } from '../../core/auth/auth-service';
import { ChatModels } from './chat-models';

const model = (id: string, efforts = ['auto', 'high']) => ({
  id,
  name: id,
  supportsImages: false,
  reasoningEfforts: efforts,
  defaultReasoningEffort: efforts[0],
});
const catalog = (allowModelSelection: boolean, defaultModelId: string | null) =>
  ({
    models: [model('a'), model('b', ['low'])],
    policy: { allowModelSelection, showModelNames: true, defaultModelId, maxInputCharacters: 100 },
    notice: null,
  }) as unknown as Models;

function setup(webSearchStatus = vi.fn()) {
  const generation = vi.fn(() => 1);
  TestBed.configureTestingModule({
    providers: [
      { provide: NexusApi, useValue: { webSearchStatus } },
      { provide: AuthService, useValue: { generation } },
    ],
  });
  return { models: TestBed.inject(ChatModels), generation };
}

describe('chat models', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('prefers the user default only when policy allows a choice', () => {
    const { models } = setup();
    models.adopt(catalog(true, 'a'), 'b', 'auto');
    expect(models.modelId()).toBe('b');
    models.adopt(catalog(false, 'a'), 'b', 'auto');
    expect(models.modelId()).toBe('a');
    models.adopt(catalog(true, 'missing'), 'gone', 'auto');
    expect(models.modelId()).toBe('a');
  });

  it("keeps the user's effort only when the chosen model supports it", () => {
    const { models } = setup();
    models.adopt(catalog(true, null), 'a', 'high');
    expect(models.reasoningEffort()).toBe('high');
    models.adopt(catalog(true, null), 'b', 'high');
    expect(models.reasoningEffort()).toBe('low');
  });

  it('refuses a choice the policy fixes or the catalog lacks', () => {
    const { models } = setup();
    models.adopt(catalog(false, 'a'), null, 'auto');
    expect(models.choose('b')).toBe(false);
    models.adopt(catalog(true, 'a'), null, 'auto');
    expect(models.choose('missing')).toBe(false);
    expect(models.choose('b')).toBe(true);
    expect([models.modelId(), models.reasoningEffort()]).toEqual(['b', 'low']);
  });

  it('ignores a web search status that arrives after the identity changed', async () => {
    let resolve!: (value: unknown) => void;
    const { models, generation } = setup(vi.fn(() => new Promise((done) => (resolve = done))));
    const loading = models.loadWebSearch();
    generation.mockReturnValue(2);
    resolve({ available: true, notice: '' });
    await loading;
    expect(models.webSearchStatus().available).toBe(false);
  });
});
