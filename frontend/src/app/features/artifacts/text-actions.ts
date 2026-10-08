import { Languages, ListFilter, BookOpenText } from 'lucide';
import { EXTRA_ICONS } from '../../shared/ui/icon';

/** Shared action names, labels and icons for message and document selections. */
export const TEXT_ACTIONS = [
  { value: 'rewrite', label: '改寫', icon: 'edit' },
  { value: 'summarize', label: '摘要', icon: 'summarize' },
  { value: 'explain', label: '解釋', icon: 'explain' },
  { value: 'translate', label: '翻譯', icon: 'translate' },
] as const;
export const TEXT_ACTION_ICON_PROVIDER = {
  provide: EXTRA_ICONS,
  useValue: { translate: Languages, summarize: ListFilter, explain: BookOpenText },
};
