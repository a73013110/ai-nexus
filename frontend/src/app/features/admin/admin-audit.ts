import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { AdminApi } from './admin-api';
import type { AuditEntry, Feature, Model } from '../../core/api/types';
import { FeatureSummary } from '../../shared/ui/feature-summary';
import { SearchField } from '../../shared/ui/search-field';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { downloadBlob } from '../../shared/browser/download';
import { toCsv } from '../../shared/browser/csv';
import { formatDate, formatModelName } from '../../shared/browser/format';
import {
  auditAction,
  auditChanges,
  auditDetails,
  auditResource,
  auditResult,
  auditRejected,
} from './audit-presentation';

@Component({
  selector: 'nx-admin-audit',
  imports: [SearchField, Select, Icon, FeatureSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  templateUrl: './admin-audit.html',
})
export class AdminAudit {
  readonly features = input<readonly Feature[]>([]);
  readonly models = input<readonly Model[]>([]);
  readonly modelNames = computed(() =>
    Object.fromEntries(this.models().map((model) => [model.id, formatModelName(model)])),
  );
  readonly rows = signal<AuditEntry[]>([]);
  readonly loading = signal(false);
  readonly more = signal(false);
  readonly error = signal('');
  readonly search = signal('');
  readonly action = signal('');
  readonly result = signal('');
  readonly from = signal('');
  readonly until = signal('');
  readonly date = formatDate;
  readonly presentedRows = computed(() =>
    this.rows().map((entry) => ({
      entry,
      date: formatDate(entry.at),
      action: auditAction(entry.action),
      resource: auditResource(entry.detailsJson),
      details: auditDetails(entry.detailsJson, this.modelNames()),
      changes: auditChanges(entry.detailsJson, this.modelNames()).map((change) => ({
        ...change,
        featureBefore: change.featureIds ? this.resolveFeatures(change.featureIds.before) : null,
        featureAfter: change.featureIds ? this.resolveFeatures(change.featureIds.after) : null,
      })),
      result: auditResult(entry.action === 'billing.price.created' ? 'created' : entry.result),
      rejected: entry.action !== 'billing.price.created' && auditRejected(entry.result),
    })),
  );
  readonly actions = [
    { value: '', label: '所有動作' },
    { value: 'admin.', label: '平台管理' },
    { value: 'admin.user', label: '使用者設定' },
    { value: 'identity.test_', label: '測試身分' },
    { value: 'admin.user_roles', label: '使用者角色' },
    { value: 'admin.user_model_policy', label: '個人模型政策' },
    { value: 'admin.user_storage', label: '使用者容量上限' },
    { value: 'admin.role', label: '角色授權' },
    { value: 'admin.group', label: '群組與模型政策' },
    { value: 'admin.feature', label: '功能異動' },
    { value: 'admin.conversation_read', label: '對話內容檢視' },
    { value: 'admin.user_usage_read', label: '使用者用量檢視' },
    { value: 'project.', label: '專案與範本' },
    { value: 'knowledge.', label: '知識庫' },
    { value: 'document.', label: '文件與索引' },
    { value: 'evaluation.', label: '評測資源' },
    { value: 'quality.', label: '評測與回饋' },
    { value: 'run.', label: 'AI 生成' },
    { value: 'integration.', label: '外部資料查閱' },
    { value: 'resource.acl', label: '資源授權' },
  ];
  private resolveFeatures(ids: string[]): Feature[] {
    const catalog = new Map(this.features().map((feature) => [feature.id, feature]));
    return ids.map((id) => catalog.get(id) ?? { id, name: id, route: '' });
  }
  readonly results = [
    { value: '', label: '所有結果' },
    { value: 'success', label: '已受理／完成' },
    { value: 'saved', label: '已儲存' },
    { value: 'read', label: '已檢視' },
    { value: 'failed', label: '未完成／拒絕' },
  ];
  private readonly api = inject(AdminApi);
  private version = 0;
  private timer?: ReturnType<typeof setTimeout>;
  constructor() {
    void this.load();
    inject(DestroyRef).onDestroy(() => {
      ++this.version;
      clearTimeout(this.timer);
    });
  }
  searchChanged(value: string) {
    this.search.set(value);
    ++this.version;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.load(), 250);
  }
  filter(key: 'action' | 'result' | 'from' | 'until', value: string) {
    this[key].set(value);
    clearTimeout(this.timer);
    void this.load();
  }
  async load(append = false) {
    if (append && this.loading()) return;
    const version = ++this.version;
    this.loading.set(true);
    this.error.set('');
    if (!append) {
      this.rows.set([]);
      this.more.set(false);
    }
    try {
      const filters = {
        search: this.search(),
        action: this.action(),
        result: this.result(),
        from: this.from() ? this.from() + 'T00:00:00+08:00' : '',
        until: this.until() ? this.nextDay(this.until()) + 'T00:00:00+08:00' : '',
      };
      const rows = await this.api.audit(append ? this.rows().at(-1)?.id : undefined, filters);
      if (version !== this.version) return;
      this.rows.update((old) => (append ? [...old, ...rows] : rows));
      this.more.set(rows.length === 100);
    } catch (error) {
      if (version === this.version)
        this.error.set(error instanceof Error ? error.message : '無法載入稽核，請重試。');
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }
  export() {
    const rows = this.rows();
    downloadBlob(
      new Blob(
        [
          toCsv([
            [
              '紀錄 ID',
              '時間（台北）',
              '操作者',
              '測試身分',
              '動作',
              '資源 ID',
              '資源識別碼',
              '結果',
              '異動資訊',
            ],
            ...rows.map((row) => [
              row.id,
              this.date(row.at),
              row.actor,
              row.actingAs || '',
              row.action,
              row.resourceId,
              auditResource(row.detailsJson),
              auditResult(row.action === 'billing.price.created' ? 'created' : row.result),
              auditDetails(row.detailsJson, this.modelNames()),
            ]),
          ]),
        ],
        { type: 'text/csv;charset=utf-8' },
      ),
      `AI-Nexus-稽核-${new Date().toISOString().slice(0, 10)}-${rows.length}筆`,
      'csv',
    );
  }
  private nextDay(day: string) {
    const date = new Date(day + 'T00:00:00Z');
    date.setUTCDate(date.getUTCDate() + 1);
    return date.toISOString().slice(0, 10);
  }
}
