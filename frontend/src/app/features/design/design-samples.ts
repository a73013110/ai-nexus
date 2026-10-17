import { Injectable, inject, signal } from '@angular/core';
import type { JobDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import type { SelectOption } from '@shared/ui/select';
import { ToastService } from '@shared/ui/toast-service';

const sampleTitle = '把想法，整理成可用的成果';
export const jobStates: SelectOption[] = [
  { value: 'completed', label: '已完成' },
  { value: 'queued', label: '等待處理' },
  { value: 'failed', label: '需要重試' },
  { value: 'cancelled', label: '已取消' },
  { value: 'running', label: '處理中（示範播放）', disabled: true },
];

/**
 * 工作台各區段共用的本地狀態，由頁面提供，讓一個區段的重設能還原其他區段。
 * 這裡不會呼叫模型，也不寫入資料。
 */
@Injectable()
export class DesignSamples {
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  readonly title = signal(sampleTitle);
  readonly titleVersion = signal(0);
  readonly selection = signal('standard');
  /** 由動作選單遞增；內嵌標題範例在它改變時開始編輯。 */
  readonly editRequest = signal(0);
  readonly demo = signal<JobDto>({
    id: 'design-sample',
    subjectId: 'design-sample',
    kind: 'demo',
    label: '狀態元件示範',
    status: 'completed',
    stage: '處理完成',
    completedUnits: 3,
    totalUnits: 3,
    attempt: 1,
    cancelRequested: false,
    errorCode: null,
    errorMessage: null,
    issueCode: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  });
  private demoVersion = 0;

  restore() {
    this.titleVersion.update((item) => item + 1);
    this.title.set(sampleTitle);
    this.selection.set('standard');
    this.setState('completed');
    this.toast.success('示範已重設');
  }
  setState(status: string) {
    this.demoVersion++;
    const stage = jobStates.find((item) => item.value === status)?.label ?? '處理中';
    this.demo.update((item2) => ({
      ...item2,
      status,
      stage,
      completedUnits: status === 'completed' ? 3 : 0,
      errorMessage: status === 'failed' ? '示範：來源暫時無法使用，可重新處理。' : null,
    }));
  }
  play() {
    const version = ++this.demoVersion;
    this.demo.update((item3) => ({
      ...item3,
      status: 'running',
      stage: '讀取文件',
      completedUnits: 0,
      errorMessage: null,
    }));
    ['整理文字', '建立索引', '處理完成'].forEach((stage, index) =>
      this.scope.later(
        () => {
          if (version !== this.demoVersion) return;
          this.demo.update((item4) => ({
            ...item4,
            stage,
            completedUnits: index + 1,
            status: index === 2 ? 'completed' : 'running',
          }));
        },
        (index + 1) * 700,
        `design-stage-${index}`,
      ),
    );
  }
}
