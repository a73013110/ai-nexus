import { ClientValidationError } from '../../core/errors/safe-errors';
const bytesPerGb = 1_000_000_000;
export const maximumStorageGb = 1_000_000;

/** Convert decimal GB to integer bytes without floating-point rounding. Blank restores inheritance. */
export function parseStorageLimitGb(value: string): number | null {
  const clean = value.trim();
  if (!clean) return null;
  if (!/^\d+(?:\.\d{1,9})?$/.test(clean)) throw new ClientValidationError('storageLimit');
  const [whole, fraction = ''] = clean.split('.');
  const bytes = Number(whole) * bytesPerGb + Number(fraction.padEnd(9, '0'));
  if (!Number.isSafeInteger(bytes) || bytes < 0 || bytes > maximumStorageGb * bytesPerGb)
    throw new ClientValidationError('storageLimit');
  return bytes;
}

export function storageLimitGb(bytes: number | null | undefined): string {
  if (bytes == null) return '';
  const whole = Math.floor(bytes / bytesPerGb);
  const fraction = String(bytes % bytesPerGb)
    .padStart(9, '0')
    .replace(/0+$/, '');
  return fraction ? `${whole}.${fraction}` : String(whole);
}
