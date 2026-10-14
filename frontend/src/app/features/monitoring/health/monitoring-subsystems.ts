import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { below } from '@shared/browser/breakpoints';
import { mediaQuery } from '@shared/browser/media-query';
import { Icon } from '@shared/ui/icon';
import { Panel } from '@shared/ui/panel';
import { StatusBadge } from '@shared/ui/status-badge';
import { MonitoringStore } from '../monitoring-store';
import { factText, factTone, subsystemStatus, toneOf } from '../monitoring-format';

/**
 * One card per module that reports its own health, in the server's order. A card shows the first
 * three facts and opens the rest. On a phone, cards without a problem fold into a disclosure.
 */
@Component({
  selector: 'nx-monitoring-subsystems',
  imports: [Icon, NgTemplateOutlet, Panel, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './monitoring-subsystems.scss',
  template: `<nx-panel heading="子系統" [count]="cards().length">
    <span panel-actions class="subsystems-caption">各模組自行回報，依各自頻率更新</span>
    @if (!cards().length) {
      <p class="subsystems-empty">此版本沒有模組回報子系統狀態。</p>
    } @else if (phone()) {
      <div class="subsystems">
        @for (card of problems(); track card.id) {
          <ng-container *ngTemplateOutlet="cardView; context: { $implicit: card }" />
        }
      </div>
      @if (calm().length) {
        <details class="subsystems-more">
          <summary>
            另有 {{ calm().length }} 個子系統沒有異常<nx-icon
              class="fold-chevron"
              name="chevron-down"
            />
          </summary>
          <div class="subsystems">
            @for (card of calm(); track card.id) {
              <ng-container *ngTemplateOutlet="cardView; context: { $implicit: card }" />
            }
          </div>
        </details>
      }
    } @else {
      <div class="subsystems">
        @for (card of cards(); track card.id) {
          <ng-container *ngTemplateOutlet="cardView; context: { $implicit: card }" />
        }
      </div>
    }
    <ng-template #cardView let-card>
      <button
        type="button"
        class="subsystem"
        [attr.data-tone]="card.tone"
        [attr.aria-label]="'檢視 ' + card.name + '，' + card.statusText"
        (click)="selected.emit(card.id)"
      >
        <span class="subsystem-head"
          ><strong>{{ card.name }}</strong
          ><nx-status-badge [tone]="card.tone">{{ card.statusText }}</nx-status-badge></span
        >
        @if (card.summary) {
          <small class="subsystem-summary">{{ card.summary }}</small>
        }
        <dl>
          @for (fact of card.facts; track fact.id) {
            <div>
              <dt>{{ fact.label }}</dt>
              <dd [attr.data-tone]="fact.tone">{{ fact.text }}</dd>
            </div>
          }
        </dl>
        @if (card.more) {
          <span class="subsystem-more">另 {{ card.more }} 項 <nx-icon name="chevron" /></span>
        }
      </button>
    </ng-template>
  </nx-panel>`,
})
export class MonitoringSubsystems {
  private readonly store = inject(MonitoringStore);
  readonly selected = output<string>();
  readonly phone = mediaQuery(below('phone'));
  readonly cards = computed(() =>
    this.store.subsystems().map((s) => ({
      id: s.id,
      name: s.name,
      status: s.status,
      tone: toneOf(s.status),
      statusText: subsystemStatus(s.status),
      summary: s.summary || (s.status === 'unobserved' ? '尚未取得這個子系統的狀態。' : ''),
      facts: s.facts.slice(0, 3).map((f) => ({
        id: f.id,
        label: f.label,
        text: factText(f),
        tone: factTone(f.tone),
      })),
      more: Math.max(0, s.facts.length - 3),
    })),
  );
  readonly problems = computed(() =>
    this.cards().filter((c) => c.status === 'degraded' || c.status === 'failing'),
  );
  readonly calm = computed(() =>
    this.cards().filter((c) => c.status !== 'degraded' && c.status !== 'failing'),
  );
}
