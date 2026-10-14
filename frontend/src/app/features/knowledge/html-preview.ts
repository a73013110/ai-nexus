import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

/**
 * Sandboxed HTML preview. The iframe loads the server's `/render` route (same origin, CSP that
 * blocks external resources); `allow-same-origin` must never be added, so scripts cannot reach
 * the session. The iframe is recreated when the sandbox changes because the attribute only
 * applies on navigation.
 */
@Component({
  selector: 'nx-html-preview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './html-preview.scss',
  template: `@for (sandbox of [sandbox()]; track sandbox) {
      <iframe
        title="HTML 預覽"
        referrerpolicy="no-referrer"
        [attr.sandbox]="sandbox"
        [src]="frame()"
      ></iframe>
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
  readonly sandbox = computed(() => (this.running() ? 'allow-scripts' : ''));
  // Same-origin render URL built by the app, never user-supplied.
  readonly frame = computed(() => this.sanitizer.bypassSecurityTrustResourceUrl(this.url()));
}
