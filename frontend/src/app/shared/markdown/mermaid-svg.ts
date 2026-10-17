import DOMPurify from 'dompurify';
import { applyDiagramContrast } from './mermaid-colors';
import { applyDiagramSemantics, type DiagramSemantics } from './mermaid-theme';

/** SVG 會以惰性圖片顯示，絕不會被插入為可執行的 Markdown HTML。 */
export function sanitizeDiagramSvg(
  source: string,
  ink: string,
  background: string,
  semantics?: DiagramSemantics,
) {
  const clean = DOMPurify.sanitize(source, {
    USE_PROFILES: { svg: true, svgFilters: true, mathMl: true },
    ADD_TAGS: ['foreignObject', 'div', 'span'],
    ADD_ATTR: ['data-look'],
    ADD_URI_SAFE_ATTR: ['data-look'],
    HTML_INTEGRATION_POINTS: { foreignobject: true },
    FORBID_TAGS: ['script', 'image', 'img', 'a', 'animate', 'set'],
    FORBID_ATTR: ['href', 'xlink:href'],
    ALLOW_DATA_ATTR: false,
  });
  const document = new DOMParser().parseFromString(clean, 'image/svg+xml');
  const svg = document.documentElement;
  if (svg.localName !== 'svg' || document.querySelector('parsererror'))
    throw new Error('Invalid diagram SVG');
  // 原生 MathML 需要 foreignObject；其他作者撰寫的 HTML 仍不支援。
  for (const object of svg.querySelectorAll('foreignObject')) {
    if (!object.querySelector('math')) object.remove();
  }
  // SVG 檔案在圖片脈絡之外下載與開啟時仍是安全的。
  for (const style of svg.querySelectorAll('style')) {
    const sheet = new CSSStyleSheet();
    sheet.replaceSync(style.textContent || '');
    sanitizeRules(sheet);
    style.textContent = [...sheet.cssRules].map((rule) => rule.cssText).join('\n');
  }
  for (const element of [svg, ...svg.querySelectorAll('*')]) {
    for (const attribute of [...element.attributes]) {
      if (externalReference(attribute.value)) element.removeAttribute(attribute.name);
    }
  }
  if (semantics) applyDiagramSemantics(svg, semantics);
  applyDiagramContrast(svg, ink, background);
  const [, , width, height] = (svg.getAttribute('viewBox') || '').split(/[\s,]+/).map(Number);
  if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0)
    throw new Error('Invalid diagram dimensions');
  const title = svg.querySelector('title')?.textContent || 'Mermaid 圖表';
  const description = svg.querySelector('desc')?.textContent || '';
  svg.setAttribute('width', String(width));
  svg.setAttribute('height', String(height));
  svg.removeAttribute('style');
  return { svg: new XMLSerializer().serializeToString(svg), width, height, title, description };
}

/** 在字型與所有版面轉換都可用時，量測一次已消毒的 SVG。 */
export function measureDiagramBounds(
  diagram: ReturnType<typeof sanitizeDiagramSvg>,
  container: HTMLElement,
) {
  container.innerHTML = diagram.svg;
  const svg = container.querySelector<SVGSVGElement>('svg')!;
  const bounds = svg.getBBox();
  const current = svg.viewBox.baseVal;
  const padding = 16;
  const pointX = Math.min(current.x, bounds.x - padding),
    pointY = Math.min(current.y, bounds.y - padding);
  const right = Math.max(current.x + current.width, bounds.x + bounds.width + padding);
  const bottom = Math.max(current.y + current.height, bounds.y + bounds.height + padding);
  const width = right - pointX,
    height = bottom - pointY;
  if (![pointX, pointY, width, height].every(Number.isFinite) || width <= 0 || height <= 0)
    throw new Error('Invalid measured diagram dimensions');
  svg.setAttribute('viewBox', `${pointX} ${pointY} ${width} ${height}`);
  svg.setAttribute('width', String(width));
  svg.setAttribute('height', String(height));
  return { ...diagram, svg: new XMLSerializer().serializeToString(svg), width, height };
}

function externalReference(value: string) {
  return /url\(/i.test(value) && !/^url\(["']?#[\w:.-]+["']?\)$/i.test(value.trim());
}
function sanitizeRules(parent: CSSStyleSheet | CSSGroupingRule | CSSKeyframesRule) {
  for (let index = parent.cssRules.length - 1; index >= 0; index--) {
    const rule = parent.cssRules[index];
    if (rule instanceof CSSStyleRule || rule instanceof CSSKeyframeRule) {
      for (const property of [...rule.style]) {
        if (externalReference(rule.style.getPropertyValue(property)))
          rule.style.removeProperty(property);
      }
    } else if (rule instanceof CSSGroupingRule || rule instanceof CSSKeyframesRule)
      sanitizeRules(rule);
    else if (parent instanceof CSSKeyframesRule)
      parent.deleteRule((rule as CSSKeyframeRule).keyText);
    else parent.deleteRule(index);
  }
}
