import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { Dashboard } from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Icon } from '../../shared/ui/icon';
@Component({ selector: 'nx-workspace-flow', imports: [Icon, RouterLink], changeDetection: ChangeDetectionStrategy.OnPush, templateUrl: './workspace-flow.html', styleUrl: './workspace-flow.scss' })
export class WorkspaceFlow {
 readonly counts = input.required<Dashboard['counts']>(); readonly requests = input(0); readonly scope = input('personal');
 readonly scopeLabel = input('我的工作');
 readonly embeddingMode = input(''); readonly webSearchAvailable = input(false); readonly giteaAvailable = input(false);
 readonly session = inject(WorkspaceSession); readonly selectedNode = signal('knowledge');
  readonly selected = computed(() => this.nodes().find(x => x.id === this.selectedNode()) ?? this.nodes()[1]);
  readonly nodes = computed(() => {
    const c = this.counts();
    return [
      { id: 'files', label: this.session.featureName('files'), icon: 'document', count: c.files, unit: '個檔案', route: '/files', feature: 'files', detail: '已保存的原檔，包含對話附件、知識庫與專案使用的檔案。同一原檔只計一次。', meta: '文件處理紀錄與索引分段另外統計。', x: 20, y: 26 },
      { id: 'knowledge', label: this.session.featureName('knowledge'), icon: 'library', count: c.collections, unit: '個知識庫', route: '/knowledge', feature: 'knowledge', detail: '依權限管理可檢索的來源文件；完成處理後，可供查詢與對話引用。', meta: `${c.chunks.toLocaleString()} 段索引 · ${c.staleIndexes} 份文件需重新索引`, x: 51, y: 37 },
      { id: 'model', label: 'AI 回覆生成', icon: 'lines', count: c.activeGenerations, unit: '件進行中', route: '/chat', feature: 'chat', detail: '目前等待或進行中的對話回覆生成，不包含文件處理與品質評測。', meta: `${c.conversations} 個對話 · 查詢期間共 ${this.requests()} 次模型與工具呼叫`, x: 81, y: 23 },
      { id: 'jobs', label: this.session.featureName('tasks'), icon: 'tasks', count: c.activeJobs, unit: '件進行中', route: '/tasks', feature: 'tasks', detail: '等待或進行中的文件文字擷取、索引與品質評測任務。文件處理完成不代表已加入知識庫。', meta: `${c.readyDocuments} 份文件處理完成 · ${c.failedDocuments} 份處理失敗 · ${c.failedJobs} 件任務失敗`, x: 31, y: 77 },
      { id: 'projects', label: this.session.featureName('projects'), icon: 'projects', count: c.projects, unit: '個專案', route: '/projects', feature: 'projects', detail: '集中共用指示、參考文件、提問範本與成果文件。', meta: '專案對話由各使用者個別保存。', x: 70, y: 77 },
    ];
  });
}
