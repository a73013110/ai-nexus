import type { ManagedRepositoryDto } from '@core/api/schema';
import { describe, expect, it } from 'vitest';
import { myAccess, repositoryTags } from './repository-access-presentation';
import { type RepositoryAccessQuery, matchesRepository } from './repository-access-query';

const repository = (changes: Partial<ManagedRepositoryDto> = {}): ManagedRepositoryDto => ({
  id: 'r1',
  owner: 'sample-org',
  name: 'portal',
  description: '範例入口網站',
  isPrivate: true,
  isArchived: false,
  missing: false,
  requestable: true,
  grantingTeams: null,
  stewards: [],
  myGrant: null,
  pendingInstanceId: null,
  manager: 'None',
  myDirectPermission: null,
  unregisteredCount: 0,
  ...changes,
});
const query = (changes: Partial<RepositoryAccessQuery> = {}): RepositoryAccessQuery => ({
  view: 'all',
  search: '',
  owner: '',
  state: '',
  ...changes,
});

describe('repository access query', () => {
  it('counts a direct Gitea collaborator as my access', () => {
    const direct = repository({ myDirectPermission: 'admin' });
    expect(matchesRepository(direct, query({ view: 'mine' }))).toBe(true);
    expect(matchesRepository(repository(), query({ view: 'mine' }))).toBe(false);
    expect(myAccess(direct)).toEqual({
      label: '管理',
      tone: 'warning',
      note: '未納管：在 Gitea 上直接加入',
    });
  });

  it('filters by organization, state and search text', () => {
    const item = repository({ unregisteredCount: 2, manager: 'Administrator' });
    expect(matchesRepository(item, query({ owner: 'sample-org', state: 'unregistered' }))).toBe(
      true,
    );
    expect(matchesRepository(item, query({ owner: 'other-org' }))).toBe(false);
    expect(matchesRepository(item, query({ state: 'public' }))).toBe(false);
    expect(matchesRepository(item, query({ search: 'SAMPLE-ORG/PORT' }))).toBe(true);
    expect(matchesRepository(item, query({ search: '入口' }))).toBe(true);
    expect(repositoryTags(item).map((tag) => tag.label)).toEqual(['2 位未納管']);
  });
});
