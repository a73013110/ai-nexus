import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  input,
  output,
  type TemplateRef,
} from '@angular/core';
import type { AttendancePersonDto } from '@core/api/schema';
import { nearViewport } from '@shared/browser/near-viewport';
import { DataTableColumn } from '@shared/ui/data-table';
import { StatusBadge } from '@shared/ui/status-badge';
import { presentRecord, summarizePerson, weekOf } from '../attendance-presentation';
import { GdFormList } from './gd-form-list';
import { cardKey } from './records-query';

/** 一位人員在畫面上的一段：摘要與每一天（換週的那一天標出來）。 */
export function presentGroup(person: AttendancePersonDto, today: string) {
  return {
    person,
    // 同一張卡先後有兩位只有姓名的持有人時是兩段。
    key: person.userId ?? `${cardKey(person.cardNumber!)}:${person.displayName}`,
    ...summarizePerson(person.days),
    // 換週的那一列上緣加分隔線；放假日不列時週一可能不在，所以比的是「哪一週」而不是星期幾。
    days: person.days.map((day, index, days) => ({
      ...presentRecord(day, today),
      weekStart: index > 0 && weekOf(day.date) !== weekOf(days[index - 1].date),
    })),
  };
}
export type RecordsGroup = ReturnType<typeof presentGroup>;

/**
 * 出勤紀錄裡一位人員的 `tbody`：手機卡片的段首列，與每天一列（寬版最左的人員欄跨過整段）。
 * 一頁可能是 20 人 × 一整年，全部建立會讓畫面卡住、之後每次版面計算也很慢；所以每人的列等捲到附近
 * （`nearViewport`）才建立，之前放一列估計高度的佔位，捲軸長度仍接近實際。獨立成 OnPush 元件，
 * 後面的人顯示時不必重新檢查前面已畫好的人。人員資訊的範本由頁面提供、樣式不封裝，所以這裡也不封裝。
 */
@Component({
  selector: 'tbody[nxRecordsGroup]',
  imports: [DataTableColumn, GdFormList, NgTemplateOutlet, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  styleUrl: './records-person-group.scss',
  host: { class: 'records-person-group' },
  template: `
    <tr class="records-person-row">
      <th scope="rowgroup" colspan="11">
        <ng-container
          [ngTemplateOutlet]="info()"
          [ngTemplateOutletContext]="{ $implicit: group() }"
        />
      </th>
    </tr>
    @if (!near()) {
      <tr class="records-pending" aria-hidden="true">
        <td colspan="11" [style.--records-days]="group().days.length || 1"></td>
      </tr>
    } @else {
      @for (row of group().days; track row.day.date; let first = $first) {
        <tr [class.is-weekend]="row.weekend" [class.records-week-start]="row.weekStart">
          @if (first) {
            <th scope="rowgroup" class="records-person-cell" [attr.rowspan]="group().days.length">
              <div class="records-person-card">
                <ng-container
                  [ngTemplateOutlet]="info()"
                  [ngTemplateOutletContext]="{ $implicit: group() }"
                />
              </div>
            </th>
          }
          <td nxTableColumn="date">
            <time [attr.datetime]="row.day.date">{{ row.date }}（{{ row.weekday }}）</time>
            @if (row.today) {
              <small class="records-today">今天</small>
            }
          </td>
          <td nxTableColumn="dayType" data-label="日型">
            {{ row.dayType }}
            @if (row.remark) {
              <small class="records-remark" [title]="row.remark">{{ row.remark }}</small>
            }
          </td>
          <!-- 標籤寫發生什麼事、顏色跟著補足狀態；原因在 title，只有真的無法判定才另寫一行。 -->
          <td nxTableColumn="status">
            <span class="records-badges">
              @for (status of row.statuses; track status.label) {
                <nx-status-badge [tone]="status.tone" [attr.title]="row.reason || null">{{
                  status.label
                }}</nx-status-badge>
              }
            </span>
            @if (row.note) {
              <small class="records-reason">{{ row.note }}</small>
            }
          </td>
          <!-- 遲到的上班、早退的下班直接在時間上標色（舊系統的紅字，顏色跟著補足狀態）；沒刷卡也沒表單補足標「未刷卡」。 -->
          <td nxTableColumn="clockIn" class="records-time" data-label="上班">
            @if (row.clockIn) {
              <span
                [class.records-time-flag]="row.late"
                [attr.data-tone]="row.coverage?.tone"
                [attr.title]="row.lateNote || null"
                >{{ row.clockIn }}</span
              >
            } @else if (row.missing) {
              <span class="records-missing">未刷卡</span>
            } @else {
              —
            }
          </td>
          <td nxTableColumn="clockOut" class="records-time" data-label="下班">
            @if (row.clockOut && !row.singlePunch) {
              <span
                [class.records-time-flag]="row.early"
                [attr.data-tone]="row.coverage?.tone"
                [attr.title]="row.earlyNote || null"
                >{{ row.clockOut }}</span
              >
            } @else if (row.missing) {
              <span class="records-missing">未刷卡</span>
            } @else {
              —
            }
          </td>
          <td nxTableColumn="expected" class="records-time" data-label="應下班">
            {{ row.expected || '—' }}
            <!-- 原本的應下班照舊系統；扣掉核准表單後的時間另起一行，走在它之後就補足。 -->
            @if (row.adjusted) {
              <small class="records-adjusted" title="扣掉核准表單時數後的應下班"
                >扣表單 {{ row.adjusted }}</small
              >
            }
          </td>
          <!-- 遲到、早退的時數照舊系統；顏色跟著當天的補足狀態（已補足綠、流程中黃、未補足紅）。 -->
          <td nxTableColumn="late" data-label="遲到">
            @if (row.late) {
              <span class="records-gap" [attr.data-tone]="row.coverage?.tone">{{ row.late }}</span>
            } @else {
              <span class="records-none">—</span>
            }
          </td>
          <td nxTableColumn="early" data-label="早退">
            @if (row.early) {
              <span class="records-gap" [attr.data-tone]="row.coverage?.tone">{{ row.early }}</span>
            } @else {
              <span class="records-none">—</span>
            }
          </td>
          <!-- HR 要追的：狀態標籤在前，接著還差多少（差額整段不斷行）、缺在哪幾段，緊鄰表單欄好對照流程中假單的起訖。 -->
          <td nxTableColumn="coverage" data-label="補足">
            @if (row.coverage; as coverage) {
              <div class="records-gap-cell" [attr.data-tone]="coverage.tone">
                <span class="records-gap-head">
                  <nx-status-badge [tone]="coverage.tone">{{ coverage.label }}</nx-status-badge>
                  @if (coverage.short) {
                    <strong class="records-short">{{ coverage.short }}</strong>
                  } @else if (coverage.formHours) {
                    <span class="records-form-hours">{{ coverage.formHours }}</span>
                  }
                </span>
                @if (coverage.gaps.length) {
                  <ul class="records-gaps" aria-label="缺口時段">
                    @for (gap of coverage.gaps; track gap.kind) {
                      <li>
                        <span>{{ gap.kind }}</span
                        ><time>{{ gap.time }}</time>
                      </li>
                    }
                  </ul>
                }
              </div>
            } @else {
              <span class="records-none">—</span>
            }
          </td>
          <td nxTableColumn="forms" data-label="表單">
            @if (row.formsUnavailable) {
              <span class="records-unavailable">無法取得</span>
            } @else if (row.forms.length) {
              <nx-gd-form-list
                [forms]="row.forms"
                (open)="openForm.emit({ desNo: $event, date: row.day.date })"
              />
            } @else {
              <!-- 大多數日子沒有表單：不建立清單元件，幾千列時省下可觀的建立時間。 -->
              <span class="records-none">—</span>
            }
          </td>
        </tr>
      } @empty {
        <!-- 區間內只有沒列出的放假日：仍列出這個人，免得寬版漏掉。 -->
        <tr>
          <th scope="rowgroup" class="records-person-cell">
            <div class="records-person-card">
              <ng-container
                [ngTemplateOutlet]="info()"
                [ngTemplateOutletContext]="{ $implicit: group() }"
              />
            </div>
          </th>
          <td colspan="10" class="records-none">這段期間沒有要列出的日子</td>
        </tr>
      }
    }
  `,
})
export class RecordsPersonGroup {
  readonly group = input.required<RecordsGroup>();
  /** 人員資訊的範本（姓名、標籤、天數），寬版人員欄與手機段首共用，由頁面提供。 */
  readonly info = input.required<TemplateRef<{ $implicit: RecordsGroup }>>();
  readonly openForm = output<{ desNo: string; date: string }>();
  protected readonly near = nearViewport();
}
