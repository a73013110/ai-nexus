import type { MermaidConfig } from 'mermaid';

export interface DiagramPalette {
  fill: string;
  stroke: string;
}
export type DiagramSemantics = Record<string, DiagramPalette>;

/** The application's tokens own diagram styling, including common semantic class names. */
export function diagramTheme(style: CSSStyleDeclaration) {
  const token = (name: string) => style.getPropertyValue(name).trim();
  const ink = token('--ink'),
    line = token('--line'),
    surface = token('--surface');
  const config: MermaidConfig = {
    theme: 'base',
    look: 'neo',
    layout: 'elk',
    fontFamily: token('--font-ui'),
    flowchart: {
      curve: 'basis',
      nodeSpacing: 40,
      rankSpacing: 52,
      padding: 16,
      diagramPadding: 24,
      wrappingWidth: 200,
    },
    themeVariables: {
      darkMode: style.colorScheme === 'dark',
      fontFamily: token('--font-ui'),
      fontSize: '14px',
      background: surface,
      primaryColor: token('--accent-soft'),
      primaryTextColor: ink,
      primaryBorderColor: line,
      nodeBorder: line,
      secondaryColor: surface,
      tertiaryColor: token('--canvas'),
      secondaryTextColor: ink,
      tertiaryTextColor: ink,
      secondaryBorderColor: line,
      tertiaryBorderColor: line,
      lineColor: token('--secondary'),
      textColor: ink,
      nodeTextColor: ink,
      clusterBkg: token('--canvas'),
      clusterBorder: line,
      edgeLabelBackground: surface,
      actorBkg: token('--accent-soft'),
      actorBorder: line,
      actorTextColor: ink,
      actorLineColor: line,
      signalColor: token('--secondary'),
      signalTextColor: ink,
      noteBkgColor: token('--canvas'),
      noteTextColor: ink,
      noteBorderColor: line,
      labelTextColor: ink,
      useGradient: false,
      dropShadow: 'none',
      strokeWidth: 1.25,
    },
  };
  const semantics: DiagramSemantics = {
    process: { fill: token('--accent-soft'), stroke: token('--accent') },
    decision: { fill: token('--canvas'), stroke: token('--secondary') },
    error: { fill: token('--error-bg'), stroke: token('--error') },
    result: { fill: token('--canvas'), stroke: token('--success') },
  };
  return { config, semantics };
}

export function applyDiagramSemantics(svg: Element, palette: DiagramSemantics) {
  for (const node of svg.querySelectorAll<SVGElement>('.node')) {
    const semantic = Object.keys(palette).find((name) => node.classList.contains(name));
    for (const shape of node.querySelectorAll<SVGElement>('.label-container')) {
      shape.style.setProperty('stroke-width', '1.25px', 'important');
      if (semantic) {
        shape.style.setProperty('fill', palette[semantic].fill, 'important');
        shape.style.setProperty('stroke', palette[semantic].stroke, 'important');
      }
      // Round rectangular process nodes; preserve decision, terminal and specialized geometry.
      if (shape.localName === 'rect' && !Number(shape.getAttribute('rx'))) {
        shape.setAttribute('rx', '7');
        shape.setAttribute('ry', '7');
      }
    }
  }
}
