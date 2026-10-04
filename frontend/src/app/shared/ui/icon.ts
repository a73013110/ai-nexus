import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const paths: Record<string, string> = {
  'thumb-up': 'M7 10v11H3V10h4Zm0 1 5-8c3 0 3 2 2 6h5a2 2 0 0 1 2 2l-2 8a2 2 0 0 1-2 2H7',
  'thumb-down': 'M7 14V3H3v11h4Zm0-1 5 8c3 0 3-2 2-6h5a2 2 0 0 0 2-2l-2-8a2 2 0 0 0-2-2H7',
  share: 'M9 11l6-5M9 13l6 5M7 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM18 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM18 22a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z',
  plus: 'M12 5v14M5 12h14',
  star: 'm12 3 2.8 5.7 6.3.9-4.5 4.4 1.1 6.3-5.7-3-5.7 3 1.1-6.3L3.2 9.6l6.3-.9L12 3Z',
  archive: 'M3 4h18v4H3zM5 8v12h14V8M10 12h4',
  restore: 'M5 8v12h14V8M3 4h18v4H3zM12 17v-5m-3 3 3-3 3 3',
  upload: 'M12 16V3m-5 5 5-5 5 5M4 15v6h16v-6',
  paperclip: 'm8 12 7-7a4 4 0 1 1 6 6L10 22a6 6 0 0 1-8-8L13 3m-7 12 9-9a2 2 0 0 1 3 3l-9 9',
  library: 'M3 4h5v16H3zM9 4h5v16H9zm8 0 4 1-3 15-4-1z',
  command: 'M9 9V5a2 2 0 1 0-2 2h10a2 2 0 1 0-2-2v14a2 2 0 1 0 2-2H7a2 2 0 1 0 2 2V9Z',
  sliders: 'M4 7h3m4 0h9M4 17h9m4 0h3M7 4h4v6H7zM13 14h4v6h-4z',
  tag: 'M3 3h8l10 10-8 8L3 11V3ZM7 7h.01',
  search: 'm21 21-4.5-4.5M10.5 18a7.5 7.5 0 1 0 0-15 7.5 7.5 0 0 0 0 15Z',
  sidebar: 'M9 3v18M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z',
  arrow: 'M12 19V5m-6 6 6-6 6 6',
  down: 'M12 5v14m-6-6 6 6 6-6',
  chevron: 'm9 5 7 7-7 7',
  left: 'm15 5-7 7 7 7',
  more: 'M5 12h.01M12 12h.01M19 12h.01',
  copy: 'M9 9h11v11H9V9ZM4 15H3V3h12v1',
  edit: 'm16 3 5 5-12 12-6 1 1-6L16 3Zm-2 2 5 5',
  repeat: 'M20 7v5h-5M4 17v-5h5M5 7a8 8 0 0 1 13-2l2 2M4 17l2 2a8 8 0 0 0 13-2',
  stop: 'M6 6h12v12H6z',
  close: 'm6 6 12 12M6 18 18 6',
  check: 'm5 12 4 4L19 6',
  document: 'M14 3H5v18h14V8l-5-5Zm0 0v5h5M8 12h8M8 16h6',
  lines: 'M4 6h16M4 12h12M4 18h8',
  idea: 'M9 18h6M10 22h4M8 14a7 7 0 1 1 8 0c-1 1-1 2-1 4H9c0-2 0-3-1-4Z',
  info: 'M12 11v6M12 7h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
  trash: 'M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7M14 10v7',
  lock: 'M6 10h12v11H6V10Zm3 0V6a3 3 0 0 1 6 0v4',
  download: 'M12 3v12m-5-5 5 5 5-5M4 15v6h16v-6',
};
@Component({
  selector: 'nx-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template:
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="path()" /></svg>',
  styles: ':host{display:inline-flex;width:20px;height:20px;flex:none}svg{width:100%;height:100%}',
})
export class Icon {
  readonly name = input.required<string>();
  readonly path = computed(() => paths[this.name()] ?? paths['info']);
}
