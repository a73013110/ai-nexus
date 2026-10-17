import { LruCache } from './lru-cache';

type Highlighter = typeof import('./code-highlight').highlightCode;

let highlighter: Promise<Highlighter | null> | null = null;
let observer: IntersectionObserver | null | undefined;
let flushing = false;
const queue = new Set<HTMLElement>();
// 重新掛載（串流轉最終回答、切換對話）時，會在繪製前重新套用顏色，而不是閃現純文字。
// null 代表沒有文法的語言。最多約 2 MB 的 UTF-16。
const highlighted = new LruCache<string, string | null>({
  maxEntries: 512,
  maxWeight: 1_000_000,
  weigh: (html, key) => key.length + (html?.length ?? 0),
});
const cacheKey = (block: HTMLElement) =>
  `${block.dataset['language'] ?? ''}\n${block.textContent ?? ''}`;

/**
 * 語法著色只是裝飾：程式碼先以跳脫後的文字渲染，只有接近視口的區塊才會以短片段上色，
 * 因此長對話開啟時不需要解析每個區塊；先前上過色的程式碼會直接從快取上色。
 * 回傳的清理函式，用於處理在抵達之前就被替換掉的區塊。
 */
export function highlightWhenVisible(root: HTMLElement): () => void {
  const blocks = [...root.querySelectorAll<HTMLElement>('code[data-language]')];
  if (!blocks.length) return () => undefined;
  observer ??=
    typeof IntersectionObserver === 'undefined'
      ? null
      : new IntersectionObserver(reached, {
          rootMargin: '600px 0px',
          scrollMargin: '600px 0px',
        } as IntersectionObserverInit);
  const pending = blocks.filter((block) => {
    const html = highlighted.get(cacheKey(block));
    if (html === undefined) return true;
    show(block, html);
    return false;
  });
  for (const block of pending) {
    if (observer) observer.observe(block);
    else enqueue(block);
  }
  return () => {
    for (const block of pending) {
      observer?.unobserve(block);
      queue.delete(block);
    }
  };
}

function reached(entries: IntersectionObserverEntry[]) {
  for (const entry of entries)
    if (entry.isIntersecting) {
      observer?.unobserve(entry.target);
      enqueue(entry.target as HTMLElement);
    }
}

function enqueue(block: HTMLElement) {
  queue.add(block);
  if (!flushing) {
    flushing = true;
    void flush();
  }
}

async function flush() {
  highlighter ??= import('./code-highlight').then(
    (module) => module.highlightCode,
    () => null,
  );
  const highlight = await highlighter;
  const deadline = performance.now() + 8;
  for (const block of queue) {
    queue.delete(block);
    if (highlight && block.isConnected) apply(block, highlight);
    if (performance.now() > deadline) break;
  }
  if (queue.size) setTimeout(() => void flush());
  else flushing = false;
}

function apply(block: HTMLElement, highlight: Highlighter) {
  const key = cacheKey(block);
  const html = highlight(block.dataset['language'] ?? '', block.textContent ?? '');
  highlighted.set(key, html);
  show(block, html);
}

function show(block: HTMLElement, html: string | null) {
  block.removeAttribute('data-language');
  if (html !== null) block.innerHTML = html;
}
