export const formatDate = (value: string) =>
  new Intl.DateTimeFormat('zh-TW', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Taipei',
  }).format(new Date(value));
export const formatNumber = (value: number) => value.toLocaleString('zh-TW');
export const formatBytes = (value: number) =>
  value < 1048576
    ? `${formatNumber(Math.ceil(value / 1024))} KB`
    : `${(value / 1048576).toFixed(1)} MB`;
