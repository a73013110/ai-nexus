/** Shared information architecture for navigation, grants and access previews. */
export const WORKSPACE_FEATURE_GROUPS = [
  {
    id: 'work',
    name: '工作',
    ids: ['dashboard', 'chat', 'files', 'projects', 'knowledge', 'artifacts'],
  },
  { id: 'collaboration', name: '協作與品質', ids: ['shared', 'quality', 'tasks', 'repositories'] },
  {
    id: 'system',
    name: '系統',
    ids: [
      'integrations',
      'admin',
      'monitoring',
      'audit',
      'logs.query',
      'logs.detail',
      'logs.export',
    ],
  },
] as const;

const membership = new Map<string, string>(
  WORKSPACE_FEATURE_GROUPS.flatMap((group) => group.ids.map((id) => [id, group.id] as const)),
);

export function groupFeatures<T extends { id: string }>(features: readonly T[]) {
  const buckets = new Map<string, T[]>();
  for (const feature of features) {
    const key = membership.get(feature.id) ?? 'additional';
    const bucket = buckets.get(key) ?? [];
    bucket.push(feature);
    buckets.set(key, bucket);
  }
  return [...WORKSPACE_FEATURE_GROUPS, { id: 'additional', name: '更多工具' }]
    .map((group) => ({ id: group.id, name: group.name, features: buckets.get(group.id) ?? [] }))
    .filter((group) => group.features.length > 0);
}

export const FEATURE_ICONS: Record<string, string> = {
  files: 'files',
  dashboard: 'dashboard',
  repositories: 'git',
  chat: 'chat',
  projects: 'projects',
  knowledge: 'library',
  artifacts: 'document',
  tasks: 'tasks',
  quality: 'shield',
  admin: 'lock',
  monitoring: 'activity',
  audit: 'audit',
  'logs.query': 'logs',
  'logs.detail': 'search',
  'logs.export': 'download',
  integrations: 'integrations',
  shared: 'share',
};
