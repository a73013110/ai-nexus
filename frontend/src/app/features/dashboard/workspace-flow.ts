import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { Dashboard } from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Icon } from '../../shared/ui/icon';
@Component({ selector: 'nx-workspace-flow', imports: [Icon, RouterLink], changeDetection: ChangeDetectionStrategy.OnPush, templateUrl: './workspace-flow.html', styleUrl: './workspace-flow.scss' })
export class WorkspaceFlow {
 readonly counts = input.required<Dashboard['counts']>(); readonly requests = input(0); readonly scope = input('personal');
 readonly embeddingMode = input(''); readonly webSearchAvailable = input(false); readonly giteaAvailable = input(false);
 readonly session = inject(WorkspaceSession); readonly selectedNode = signal('knowledge');
  readonly selected = computed(() => this.nodes().find(x => x.id === this.selectedNode()) ?? this.nodes()[1]);
  readonly nodes = computed(() => {
    const c = this.counts();
    return [
      { id: 'documents', label: '文件', icon: 'document', count: c?.documents ?? 0, route: '/knowledge', feature: 'knowledge', detail: '已上傳、匯入與讀取的文件。', meta: `${c?.failedDocuments ?? 0} 份需要處理`, x: 20, y: 26 },
      { id: 'knowledge', label: '知識索引', icon: 'database', count: c?.chunks ?? 0, route: '/knowledge', feature: 'knowledge', detail: '將文件分段、向量化，再依權限找出回答所需的內容。', meta: `${c?.readyDocuments ?? 0} 份就緒 · ${c?.staleIndexes ?? 0} 份需重新索引`, x: 51, y: 37 },
      { id: 'model', label: 'AI 處理', icon: 'lines', count: c?.activeGenerations ?? 0, route: '/chat', feature: 'chat', detail: '對話、文字處理與評測共用模型呼叫與費用紀錄。', meta: `${this.requests() ?? 0} 次區間呼叫`, x: 81, y: 23 },
      { id: 'jobs', label: '背景任務', icon: 'tasks', count: c?.activeJobs ?? 0, route: '/tasks', feature: 'tasks', detail: '文件索引與品質評測的實際處理狀態。', meta: `${c?.failedJobs ?? 0} 件未完成`, x: 31, y: 77 },
      { id: 'projects', label: '專案', icon: 'projects', count: c?.projects ?? 0, route: '/projects', feature: 'projects', detail: '共用指示、參考文件與協作脈絡。', meta: `${c?.collections ?? 0} 個知識庫`, x: 70, y: 77 },
    ];
  });
}
