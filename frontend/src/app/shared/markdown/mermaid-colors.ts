/** Keep authored solid node colors while making their SVG labels readable in either theme. */
export function applyDiagramContrast(svg: Element, ink: string, background: string) {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 1;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  if (!context) return;
  const colors = new Map<string, number[]>();
  const rgb = (value: string) => {
    if (!CSS.supports('color', value) || /currentcolor|var\(/i.test(value)) return null;
    if (!colors.has(value)) {
      context.clearRect(0, 0, 1, 1);
      context.fillStyle = value;
      context.fillRect(0, 0, 1, 1);
      colors.set(value, [...context.getImageData(0, 0, 1, 1).data]);
    }
    return colors.get(value)!;
  };
  const surface = rgb(background),
    foreground = rgb(ink);
  if (!surface || !foreground) return;
  const foregroundLight = luminance(foreground);
  for (const node of svg.querySelectorAll<SVGElement>('.node')) {
    const shape = node.querySelector<SVGElement>('.label-container');
    if (!shape) continue;
    const fill = rgb(shape.style.fill || shape.getAttribute('fill') || '');
    if (!fill) continue;
    const authoredOpacity = shape.style.fillOpacity || '1';
    const opacity =
      (fill[3] / 255) *
      Math.max(
        0,
        Math.min(1, parseFloat(authoredOpacity) / (authoredOpacity.endsWith('%') ? 100 : 1)),
      );
    const light = luminance(
      fill.map((channel, i) => channel * opacity + surface[i] * (1 - opacity)),
    );
    const contrast =
      (Math.max(light, foregroundLight) + 0.05) / (Math.min(light, foregroundLight) + 0.05);
    const color =
      contrast >= 4.5
        ? ink
        : (light + 0.05) / 0.05 >= 1.05 / (light + 0.05)
          ? '#000000'
          : '#ffffff';
    for (const label of node.querySelectorAll<SVGElement>('.label text, .label tspan'))
      label.style.setProperty('fill', color, 'important');
  }
}

function luminance(rgb: readonly number[]) {
  const linear = rgb.slice(0, 3).map((value) => {
    const channel = value / 255;
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
}
