import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

/**
 * 沙盒 HTML 預覽。iframe 載入伺服器的 `/render` 路由（同源、CSP 封鎖外部資源）；絕不加
 * `allow-same-origin`，指令碼因此碰不到登入狀態。
 * Angular 只接受靜態的 iframe `sandbox`（動態綁定會丟 NG0910），所以兩種權限各寫一個分支：
 * 切換時 @if 銷毀舊 iframe 再建立新的，sandbox 在新 iframe 第一次導覽前就已生效。
 */
@Component({
  selector: 'nx-html-preview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './html-preview.scss',
  template: `@if (running()) {
      <iframe
        title="HTML 預覽"
        referrerpolicy="no-referrer"
        sandbox="allow-scripts"
        [src]="frame()"
      ></iframe>
    } @else {
      <iframe title="HTML 預覽" referrerpolicy="no-referrer" sandbox="" [src]="frame()"></iframe>
    }
    @if (gated()) {
      <div class="html-gate-backdrop">
        <section class="html-gate" aria-labelledby="html-gate-title">
          <h3 id="html-gate-title">這份 HTML 含有指令碼</h3>
          <p>
            指令碼會在隔離環境執行：讀不到你的登入狀態、無法呼叫 AI
            Nexus，也無法連到外部網站。只執行你信任的檔案。
          </p>
          <div class="html-gate-actions">
            <button type="button" class="quiet-button" (click)="viewSource.emit()">
              檢視原始碼
            </button>
            <button type="button" class="primary-button" (click)="run.emit()">執行指令碼</button>
          </div>
        </section>
      </div>
    }
    @if (running()) {
      <span class="html-sandbox-pill">在沙盒內執行 · 無法存取帳號與網路</span>
    }`,
})
export class HtmlPreview {
  private readonly sanitizer = inject(DomSanitizer);
  readonly url = input.required<string>();
  readonly gated = input(false);
  readonly running = input(false);
  readonly run = output<void>();
  readonly viewSource = output<void>();
  // Same-origin render URL built by the app, never user-supplied.
  readonly frame = computed(() => this.sanitizer.bypassSecurityTrustResourceUrl(this.url()));
}
