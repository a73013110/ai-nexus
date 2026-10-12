import {
  afterNextRender,
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ThemeService } from '../../../core/preferences/theme-service';
import { NumberTicker } from '../../../shared/ui/number-ticker';
import { DashboardStore } from '../dashboard-store';
import { compactNumber, metricLabel, sectionIcon, workspaceLink } from '../dashboard-format';
import type { OrbitScene } from './orbit-scene';
import { OrbitPanel } from './orbit-panel';

const PALETTE = ['var(--series-1)', 'var(--series-5)', 'var(--series-2)', 'var(--series-6)'];
const STATE_TEXT: Record<string, string> = {
  active: '處理中',
  attention: '需要留意',
  idle: '待命',
};

/**
 * A 3D map of the workspace: each feature orbits the core, sized by its headline figure, glowing
 * while work moves through it. three.js loads only with this style; without WebGL the features sit
 * on a flat ring. Labels are real buttons, so the map works by keyboard and screen reader.
 */
@Component({
  selector: 'nx-orbit-hero',
  imports: [NumberTicker, OrbitPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './orbit-hero.scss',
  host: { '[class.is-flat]': 'flat()' },
  template: `<canvas #canvas aria-hidden="true"></canvas>
    @if (flat()) {
      <div class="orbit-flat-core" aria-hidden="true"><i></i></div>
    }
    <div class="orbit-labels" #labels role="group" aria-label="功能星圖">
      @for (node of nodes(); track node.id) {
        <button
          type="button"
          class="orbit-label"
          [attr.data-node]="node.id"
          [attr.data-state]="node.state"
          [attr.aria-pressed]="selected() === node.id"
          [style.--node]="node.color"
          [style.left.%]="flat() ? node.flatX : null"
          [style.top.%]="flat() ? node.flatY : null"
          (click)="selected.set(selected() === node.id ? null : node.id)"
        >
          <i aria-hidden="true"></i>{{ node.title }}<b>{{ node.figure }}</b>
        </button>
      }
    </div>
    <header class="orbit-hud">
      <span class="orbit-eyebrow">NEXUS CORE · {{ store.scopeLabel() }}</span>
      <h2>
        <nx-number-ticker [value]="store.data()?.spend?.requests ?? 0" [format]="compact" />
        <small>次模型與工具呼叫</small>
      </h2>
      <p [attr.data-state]="store.activeWork() ? 'active' : 'idle'">
        <i aria-hidden="true"></i
        >{{ store.activeWork() ? store.activeWork() + ' 件工作處理中' : '核心待命中' }}
      </p>
    </header>
    @if (detail(); as d) {
      <nx-orbit-panel [detail]="d" />
    } @else {
      <p class="orbit-hint">點選星體查看功能狀態</p>
    }`,
})
export class OrbitHero {
  readonly store = inject(DashboardStore);
  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  readonly labels = viewChild.required<ElementRef<HTMLElement>>('labels');
  readonly compact = compactNumber;
  readonly selected = signal<string | null>(null);
  readonly flat = signal(false);
  private readonly scene = signal<OrbitScene | null>(null);
  private labelElements = new Map<string, HTMLElement>();

  readonly nodes = computed(() => {
    const sections = this.store.sections().filter((x) => x.id !== 'notifications');
    const figures = sections.map((x) => x.metrics[0]?.value ?? 0);
    const max = Math.log10(1 + Math.max(1, ...figures));
    return sections.map((section, i) => {
      const value = section.metrics[0]?.value ?? 0;
      const angle = (i / sections.length) * Math.PI * 2 - Math.PI / 2;
      return {
        id: section.id,
        title: section.title,
        state: section.state,
        figure: section.metrics[0] ? metricLabel(section.metrics[0]) : '',
        weight: Math.log10(1 + Math.max(0, value)) / max,
        color:
          section.state === 'attention'
            ? 'var(--series-4)'
            : section.state === 'active'
              ? 'var(--orbit-active)'
              : PALETTE[i % PALETTE.length],
        flatX: 50 + Math.cos(angle) * 40,
        flatY: 52 + Math.sin(angle) * 36,
      };
    });
  });
  readonly detail = computed(() => {
    const section = this.store.section(this.selected() ?? '');
    if (!section) return null;
    return {
      title: section.title,
      icon: sectionIcon(section.id),
      stateText: STATE_TEXT[section.state] ?? section.state,
      tone: (section.state === 'attention'
        ? 'danger'
        : section.state === 'active'
          ? 'info'
          : 'neutral') as 'danger' | 'info' | 'neutral',
      metrics: section.metrics
        .slice(0, 5)
        .map((m) => ({ key: m.key, label: m.label, text: metricLabel(m) })),
      link: this.store.session.has(section.feature) ? workspaceLink(section.route) : null,
    };
  });

  constructor() {
    const destroy = inject(DestroyRef);
    let destroyed = false;
    afterNextRender(async () => {
      try {
        const { createOrbitScene } = await import('./orbit-scene');
        if (destroyed) return;
        const scene = createOrbitScene(this.canvas().nativeElement, {
          pick: (id) => this.selected.set(id),
          place: (positions) => this.place(positions),
        });
        this.scene.set(scene);
      } catch {
        this.flat.set(true);
      }
    });
    destroy.onDestroy(() => {
      destroyed = true;
      this.scene()?.dispose();
    });
    // Colors are tokens; the canvas needs them resolved.
    effect(() => {
      const scene = this.scene(),
        nodes = this.nodes();
      if (!scene) return;
      const style = getComputedStyle(this.host);
      const resolve = (value: string) =>
        value.startsWith('var(')
          ? style.getPropertyValue(value.slice(4, -1)).trim() || '#7dd3fc'
          : value;
      scene.update(
        nodes.map((x) => ({
          id: x.id,
          weight: x.weight,
          active: x.state === 'active',
          color: resolve(x.color),
        })),
        this.store.activeWork() > 0,
      );
    });
    effect(() => this.scene()?.select(this.selected()));
    effect(() => this.scene()?.setMotion(!this.theme.reducedMotion()));
    afterRenderEffect(() => {
      this.nodes();
      this.labelElements = new Map(
        [...this.labels().nativeElement.querySelectorAll<HTMLElement>('[data-node]')].map((x) => [
          x.dataset['node'] ?? '',
          x,
        ]),
      );
    });
  }

  /** Moves each label onto its body; nearer bodies draw their labels on top. */
  private place(positions: ReadonlyMap<string, { x: number; y: number; depth: number }>) {
    for (const [id, position] of positions) {
      const label = this.labelElements.get(id);
      if (!label) continue;
      label.style.translate = `${position.x.toFixed(1)}px ${position.y.toFixed(1)}px`;
      label.style.zIndex = String(Math.round((1 - position.depth) * 1000));
      label.style.opacity = position.depth > 1 ? '0' : '';
    }
  }
}
