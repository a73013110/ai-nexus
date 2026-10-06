import type { Model } from '../../core/api/types';

export const formatDate = (value: string) =>
  new Intl.DateTimeFormat('zh-TW', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Taipei',
  }).format(new Date(value));
export const formatNumber = (value: number) => value.toLocaleString('zh-TW');
export const formatBytes = (value: number) =>
  value < 1000
    ? `${formatNumber(value)} bytes`
    : value < 1_000_000
      ? `${(value / 1000).toFixed(1)} KB`
      : value < 1_000_000_000
        ? `${(value / 1_000_000).toFixed(1)} MB`
        : value < 1_000_000_000_000
          ? `${(value / 1_000_000_000).toFixed(2)} GB`
          : `${(value / 1_000_000_000_000).toFixed(2)} TB`;
export const formatDuration = (milliseconds: number) =>
  milliseconds < 60_000
    ? `${(milliseconds / 1000).toFixed(1)} 秒`
    : `${Math.floor(milliseconds / 60_000)} 分 ${Math.floor((milliseconds % 60_000) / 1000)} 秒`;
export const formatModelName = (model: Model) =>
  model.provider
    ? `${model.displayName} · ${model.provider === 'google' ? 'Google' : model.provider === 'ollama' ? 'Ollama' : model.provider}`
    : model.displayName;

export function formatModelId(id: string) {
  return id === 'retired-model' ? '已停用的模型' : id.replace(/^model-(\d+)$/, '模型 $1');
}
