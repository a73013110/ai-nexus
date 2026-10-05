const bytesPerGb = 1_000_000_000;
export const maximumStorageGb = 1_000_000;

/** Convert decimal GB to integer bytes without floating-point rounding. Blank restores inheritance. */
export function parseStorageLimitGb(value: string): number | null {
  const clean = value.trim();
  if (!clean) return null;
  if (!/^\d+(?:\.\d{1,9})?$/.test(clean))
    throw new Error('容量請輸入 0 至 1,000,000 GB，最多九位小數；留空可恢復群組或預設上限。');
  const [whole, fraction = ''] = clean.split('.');
  const bytes = Number(whole) * bytesPerGb + Number(fraction.padEnd(9, '0'));
  if (!Number.isSafeInteger(bytes) || bytes < 0 || bytes > maximumStorageGb * bytesPerGb)
    throw new Error('容量上限需介於 0 至 1,000,000 GB。');
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
