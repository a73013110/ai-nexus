import { ChangeDetectionStrategy, Component, signal, viewChild } from '@angular/core';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { ApiClientGuideBody } from './api-client-guide-body';

/**
 * 外部系統頁的「串接說明」：給對方開發者的呼叫方式、回應處理與範例程式，列表旁的抽屜，不擋住列表操作。
 * 內容第一次打開才載入（`@defer`），說明、範例原文與語法上色不進頁面的初始程式。
 */
@Component({
  selector: 'nx-api-client-guide',
  imports: [ApiClientGuideBody, DetailDrawer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-detail-drawer title="串接說明" preferenceKey="api-client-guide">
    @if (opened()) {
      @defer {
        <nx-api-client-guide-body />
      } @placeholder {
        <p role="status">正在載入串接說明…</p>
      }
    }
  </nx-detail-drawer>`,
})
export class ApiClientGuide {
  readonly opened = signal(false);
  private readonly drawer = viewChild.required(DetailDrawer);

  open() {
    this.opened.set(true);
    this.drawer().open();
  }
}
