import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { AttendancePersonDto, AttendanceRuleDto } from '@core/api/schema';
import { anchoredPopover } from '@shared/browser/anchored-popover';
import { positionPopover } from '@shared/browser/popover-position';
import { uniqueId } from '@shared/browser/unique-id';
import { StatusBadge } from '@shared/ui/status-badge';
import { AttendanceApi } from '../attendance-api';
import { attendanceProblem, presentExplanation, weekday } from '../attendance-presentation';

/** 要說明的那一天：誰（使用者以 ID、只有姓名的持有人以卡號）與哪一天。 */
interface ExplanationTarget {
  query: { date: string; userId?: string; card?: string };
  name: string;
  day: string;
}

/**
 * 出勤紀錄一天的「判定過程」浮層：刷卡、應下班、上班時數與表單一步步怎麼得出結論（後端 `AttendanceExplanation`），
 * 加上依據的規則條文。整頁只有一個，點哪一天的狀態就錨在哪裡（`open`），列數上千時不必每列各建一個浮層。
 * 數字與文字都由後端依判定寫好，畫面只排版；讀取失敗不留「正在載入」。
 */
@Component({
  selector: 'nx-record-explanation',
  imports: [StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './record-explanation.scss',
  template: `<div
    #panel
    class="popover-panel record-explanation ui-density-compact"
    [id]="id"
    role="dialog"
    popover="auto"
    tabindex="-1"
    [attr.aria-labelledby]="id + '-heading'"
    (toggle)="toggled($event)"
    (keydown.escape)="close($event)"
  >
    @if (target(); as target) {
      <header class="record-explanation-head">
        <h2 [id]="id + '-heading'">判定過程</h2>
        <p>{{ target.name }} · {{ target.day }}</p>
      </header>
      @if (view(); as view) {
        <div class="record-explanation-body">
          <p class="record-explanation-schedule">{{ view.schedule }}</p>
          <ol class="record-explanation-steps" [attr.data-tone]="view.tone">
            @for (step of view.steps; track $index) {
              <li [attr.data-kind]="step.kind">
                <time>{{ step.time }}</time>
                <span class="record-explanation-dot" aria-hidden="true"></span>
                <span>{{ step.text }}</span>
              </li>
            }
          </ol>
          <div class="record-explanation-result">
            <nx-status-badge [tone]="view.badge.tone">{{ view.badge.label }}</nx-status-badge>
            <p>{{ view.conclusion }}</p>
          </div>
          @if (view.rules.length) {
            <details class="record-explanation-rules" (toggle)="popover.reposition()">
              <summary>依據的規則（{{ view.rules.length }} 條）</summary>
              <dl class="info-terms">
                @for (rule of view.rules; track rule.title) {
                  <div>
                    <dt>{{ rule.title }}</dt>
                    @if (rule.text) {
                      <dd>{{ rule.text }}</dd>
                    }
                  </div>
                }
              </dl>
            </details>
          }
        </div>
      } @else if (read.error()) {
        <p class="record-explanation-error" role="alert">無法取得判定過程：{{ read.error() }}</p>
      } @else {
        <div class="record-explanation-skeleton" role="status">
          <span class="sr-only">正在載入判定過程…</span>
          @for (width of skeleton; track $index) {
            <span class="skeleton" [style.width.%]="width"></span>
          }
        </div>
      }
    }
  </div>`,
})
export class RecordExplanation {
  /** 頁面已讀到的判定規則，依標題對到說明引用的條文；還沒讀到時只列標題。 */
  readonly rules = input<readonly AttendanceRuleDto[]>([]);
  readonly id = uniqueId('nx-record-explanation');
  protected readonly skeleton = [42, 88, 64, 76, 52];
  private readonly api = inject(AttendanceApi);
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  protected readonly target = signal<ExplanationTarget | null>(null);
  private anchor: HTMLElement | null = null;
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) => {
      if (this.anchor?.isConnected) positionPopover(this.anchor, panel, 380, 'left', 520);
    },
  });
  protected readonly read = apiResource({
    feature: 'attendance.records',
    params: () => this.target()?.query,
    loader: (query, signal) => this.api.explanation(query, signal),
    message: attendanceProblem,
  });
  protected readonly view = computed(() => {
    const value = this.read.value();
    // 換了一天或一個人時，上一筆說明不能留著當成這一筆：請求改變就回到骨架。
    return value && !this.read.loading() ? presentExplanation(value, this.rules()) : null;
  });

  constructor() {
    // 骨架換成說明、展開規則都會改變高度：畫完再依新高度重新放置，靠近視窗底部時才會翻到上方而不被切掉。
    afterRenderEffect(() => {
      this.view();
      this.popover.reposition();
    });
  }

  /** 在 `anchor`（那一天的狀態）旁打開 `person` 在 `date` 的判定過程；同一天再按一次就關閉。 */
  open(anchor: HTMLElement, person: AttendancePersonDto, date: string) {
    const query = person.userId
      ? { date, userId: person.userId }
      : { date, card: person.cardNumber! };
    const same = this.popover.opened() && this.anchor === anchor;
    if (same) return this.close();
    this.anchor?.removeAttribute('aria-expanded');
    this.anchor = anchor;
    anchor.setAttribute('aria-expanded', 'true');
    this.target.set({
      query,
      name: person.displayName,
      day: `${date.slice(5).replace('-', '/')}（${weekday(date)}）`,
    });
    this.popover.show(anchor);
    this.panel().nativeElement.focus();
  }

  /** 輕關閉（點外面、Escape）也要讓觸發的那一天知道已收起。 */
  protected toggled(event: Event) {
    this.popover.sync(event);
    if (!this.popover.opened()) this.anchor?.setAttribute('aria-expanded', 'false');
  }

  /** Escape 只關閉浮層，焦點回到那一天的狀態。 */
  close(event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.popover.hide();
    this.anchor?.setAttribute('aria-expanded', 'false');
    if (this.anchor?.isConnected) this.anchor.focus();
  }
}
