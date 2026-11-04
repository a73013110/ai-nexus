import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { ReaderOverlay } from '@shared/browser/reader-overlay';
import { FileList, fileKind, fileSize, type FileListItem } from './file-list';

const remote: FileListItem = {
  key: 't1',
  fileName: '診斷證明.pdf',
  detail: '林予安 上傳',
  reader: {
    id: 't1',
    attachment: false,
    remote: { ticket: 't1', fileName: '診斷證明.pdf', contentType: 'application/pdf' },
  },
  downloadUrl: '/api/v1/remote-files/content?ticket=t1&download=true',
};

function render(files: FileListItem[], editable = false) {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AuthService, useValue: {} }],
  });
  const fixture = TestBed.createComponent(FileList);
  fixture.componentRef.setInput('files', files);
  fixture.componentRef.setInput('editable', editable);
  fixture.detectChanges();
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

describe('FileList', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('opens a remote file in the reader and links its download without the source URL', () => {
    const { root } = render([remote]);
    const preview = root.querySelector<HTMLAnchorElement>('.attachment-preview')!;
    expect(preview.getAttribute('href')).toBe('/api/v1/remote-files/content?ticket=t1');
    expect(root.querySelector('.attachment-download')!.getAttribute('href')).toBe(
      remote.downloadUrl,
    );
    expect(root.querySelector('small')!.textContent).toBe('林予安 上傳');
    expect(root.querySelector('img')).toBeNull();
    expect(root.querySelector('.attachment-remove')).toBeNull();

    preview.click();
    expect(TestBed.inject(ReaderOverlay).target()?.remote?.ticket).toBe('t1');
  });

  it('shows thumbnails and emits the key of a removed file', () => {
    const { fixture, root } = render([{ ...remote, thumbnail: '/thumb' }], true);
    const removed: string[] = [];
    fixture.componentInstance.remove.subscribe((key) => removed.push(key));
    expect(root.querySelector('img')!.getAttribute('src')).toBe('/thumb');
    root.querySelector<HTMLButtonElement>('.attachment-remove')!.click();
    expect(removed).toEqual(['t1']);
    expect(root.querySelector('.attachment-remove')!.getAttribute('aria-label')).toBe(
      '移除附件：診斷證明.pdf',
    );
  });

  it('exposes the layout to its styles, cards by default', () => {
    const { fixture } = render([remote]);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.dataset['layout']).toBe('cards');
    fixture.componentRef.setInput('layout', 'tiles');
    fixture.detectChanges();
    expect(host.dataset['layout']).toBe('tiles');
  });

  it('fades a thumbnail in once loaded and falls back to the file kind when it fails', () => {
    const { fixture, root } = render([
      { ...remote, key: 'a', thumbnail: '/a' },
      { ...remote, key: 'b', thumbnail: '/b' },
    ]);
    const [first, second] = Array.from(root.querySelectorAll('img'));
    first.dispatchEvent(new Event('load'));
    second.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(root.querySelectorAll('img')).toHaveLength(1);
    expect(root.querySelector('img')!.classList).toContain('is-loaded');
    expect(root.querySelector('.attachment-kind')!.textContent).toBe('PDF');
  });

  it('names the file kind from a short extension only', () => {
    expect([
      fileKind('報告.docx'),
      fileKind('photo.JPEG'),
      fileKind('README'),
      fileKind('a.backup'),
    ]).toEqual(['DOCX', 'JPEG', '檔案', '檔案']);
  });

  it('formats sizes in KB below a megabyte', () => {
    expect([fileSize(10), fileSize(300 * 1024), fileSize(3.25 * 1024 * 1024)]).toEqual([
      '1 KB',
      '300 KB',
      '3.3 MB',
    ]);
  });
});
