import { formatMilliseconds } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';

const tones: Record<string, BadgeTone> = {
  Information: 'info',
  Warning: 'warning',
  Error: 'danger',
  Critical: 'danger',
};
export const levelTone = (level: string): BadgeTone => tones[level] ?? 'neutral';
export const milliseconds = (value: number | null) => formatMilliseconds(value, 3);
