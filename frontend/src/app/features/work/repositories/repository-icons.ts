import type { Provider } from '@angular/core';
import {
  BookOpen,
  CircleCheck,
  CircleDot,
  FileCode,
  Folder,
  GitCommitHorizontal,
  GitCompare,
  GitMerge,
  GitPullRequest,
  GitPullRequestClosed,
  KeyRound,
  PinOff,
  Sparkles,
} from 'lucide';
import { EXTRA_ICONS } from '@shared/ui/icon';

/** 儲存庫用語，保留在功能的延遲載入套件中。 */
export const repositoryIcons: Provider = {
  provide: EXTRA_ICONS,
  useValue: {
    commit: GitCommitHorizontal,
    compare: GitCompare,
    pull: GitPullRequest,
    'pull-closed': GitPullRequestClosed,
    merged: GitMerge,
    issue: CircleDot,
    'issue-closed': CircleCheck,
    folder: Folder,
    'file-code': FileCode,
    readme: BookOpen,
    key: KeyRound,
    unpin: PinOff,
    review: Sparkles,
  },
};
