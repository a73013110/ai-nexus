import DOMPurify from 'dompurify';
import { applyDiagramContrast } from './mermaid-colors';

/** SVGs are displayed as inert images, never inserted as active Markdown HTML. */
export function sanitizeDiagramSvg(source: string, ink: string, background: string) {
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
  // Native MathML needs a foreignObject; other authored HTML stays unsupported.
  for (const object of svg.querySelectorAll('foreignObject')) {
    if (!object.querySelector('math')) object.remove();
  }
  // SVG files remain safe when downloaded and opened outside the image context.
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

function externalReference(value: string) {
  return /url\(/i.test(value) && !/^url\(["']?#[\w:.-]+["']?\)$/i.test(value.trim());
}
function sanitizeRules(parent: CSSStyleSheet | CSSGroupingRule | CSSKeyframesRule) {
  for (let i = parent.cssRules.length - 1; i >= 0; i--) {
    const rule = parent.cssRules[i];
    if (rule instanceof CSSStyleRule || rule instanceof CSSKeyframeRule) {
      for (const property of [...rule.style]) {
        if (externalReference(rule.style.getPropertyValue(property)))
          rule.style.removeProperty(property);
      }
    } else if (rule instanceof CSSGroupingRule || rule instanceof CSSKeyframesRule)
      sanitizeRules(rule);
    else if (parent instanceof CSSKeyframesRule)
      parent.deleteRule((rule as CSSKeyframeRule).keyText);
    else parent.deleteRule(i);
  }
}
