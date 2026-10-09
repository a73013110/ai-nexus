type Highlighter = typeof import('./code-highlight').highlightCode;

let highlighter: Promise<Highlighter | null> | null = null;
let observer: IntersectionObserver | null | undefined;
let flushing = false;
const queue = new Set<HTMLElement>();

/**
 * Syntax colour is decoration: code first renders as escaped text, and only blocks near the
 * viewport are highlighted, in short slices, so long conversations open without parsing every
 * block. Returns a cleanup for blocks that are replaced before they were reached.
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
  for (const block of blocks) {
    if (observer) observer.observe(block);
    else enqueue(block);
  }
  return () => {
    for (const block of blocks) {
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
  const language = block.dataset['language'] ?? '';
  block.removeAttribute('data-language');
  const html = highlight(language, block.textContent ?? '');
  if (html !== null) block.innerHTML = html;
}
