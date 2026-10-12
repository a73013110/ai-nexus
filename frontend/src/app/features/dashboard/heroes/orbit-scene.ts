import {
  AdditiveBlending,
  BufferAttribute,
  BufferGeometry,
  CanvasTexture,
  Color,
  EdgesGeometry,
  Group,
  IcosahedronGeometry,
  Line,
  LineBasicMaterial,
  LineLoop,
  LineSegments,
  Mesh,
  MeshBasicMaterial,
  PerspectiveCamera,
  Points,
  PointsMaterial,
  Raycaster,
  Scene,
  SphereGeometry,
  Sprite,
  SpriteMaterial,
  Vector2,
  Vector3,
  WebGLRenderer,
  type Material,
} from 'three';

/** A feature on the map: how big it is, whether work is moving through it and its color. */
export interface OrbitNode {
  id: string;
  /** Relative size, 0–1. */
  weight: number;
  /** Work in progress: particles flow between it and the core. */
  active: boolean;
  color: string;
}
export interface OrbitOptions {
  /** Called when a node is clicked on the canvas. */
  pick: (id: string) => void;
  /** Called every frame with screen positions (CSS pixels) for the node labels. */
  place: (positions: ReadonlyMap<string, { x: number; y: number; depth: number }>) => void;
}
export interface OrbitScene {
  update(nodes: readonly OrbitNode[], coreActive: boolean): void;
  select(id: string | null): void;
  setMotion(enabled: boolean): void;
  dispose(): void;
}

const RINGS = [
  { radius: 5.2, tilt: 0.32, speed: 0.11 },
  { radius: 7.4, tilt: -0.22, speed: 0.075 },
  { radius: 9.6, tilt: 0.12, speed: 0.05 },
];
const PARTICLES = 14;

/** A soft round glow for sprites, drawn once. */
function glowTexture() {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 128;
  const context = canvas.getContext('2d')!;
  const gradient = context.createRadialGradient(64, 64, 0, 64, 64, 64);
  gradient.addColorStop(0, 'rgba(255,255,255,1)');
  gradient.addColorStop(0.25, 'rgba(255,255,255,0.55)');
  gradient.addColorStop(1, 'rgba(255,255,255,0)');
  context.fillStyle = gradient;
  context.fillRect(0, 0, 128, 128);
  return new CanvasTexture(canvas);
}

/**
 * The "Nexus core": features orbit a wireframe core on three tilted rings, sized by their
 * activity, with particles streaming along the links of features that have work in progress.
 * Throws when WebGL is unavailable; the caller then shows its flat fallback.
 */
export function createOrbitScene(canvas: HTMLCanvasElement, options: OrbitOptions): OrbitScene {
  const renderer = new WebGLRenderer({
    canvas,
    antialias: true,
    alpha: true,
    powerPreference: 'low-power',
  });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setClearColor(0x000000, 0);
  const scene = new Scene();
  const camera = new PerspectiveCamera(42, 1, 0.1, 400);
  camera.position.set(0, 5.5, 22);
  camera.lookAt(0, 0, 0);
  const glow = glowTexture();
  const disposables: { dispose(): void }[] = [glow];
  const track = <T extends { dispose(): void }>(value: T) => {
    disposables.push(value);
    return value;
  };

  // Stars: a still sphere of points far behind everything.
  const starPositions = new Float32Array(1600 * 3);
  for (let i = 0; i < 1600; i++) {
    const r = 60 + Math.random() * 90,
      theta = Math.random() * Math.PI * 2,
      phi = Math.acos(2 * Math.random() - 1);
    starPositions.set(
      [r * Math.sin(phi) * Math.cos(theta), r * Math.cos(phi), r * Math.sin(phi) * Math.sin(theta)],
      i * 3,
    );
  }
  const starGeometry = track(new BufferGeometry());
  starGeometry.setAttribute('position', new BufferAttribute(starPositions, 3));
  const stars = new Points(
    starGeometry,
    track(
      new PointsMaterial({
        size: 0.45,
        color: 0xcfe3ff,
        transparent: true,
        opacity: 0.7,
        sizeAttenuation: true,
        depthWrite: false,
      }),
    ),
  );
  scene.add(stars);

  const world = new Group();
  scene.add(world);

  // Core: a wireframe icosahedron around a bright sphere and a halo.
  const coreColor = new Color('#7dd3fc');
  const core = new Group();
  const shell = new LineSegments(
    track(new EdgesGeometry(track(new IcosahedronGeometry(1.7, 1)))),
    track(new LineBasicMaterial({ color: coreColor, transparent: true, opacity: 0.75 })),
  );
  const heart = new Mesh(
    track(new SphereGeometry(0.85, 32, 32)),
    track(new MeshBasicMaterial({ color: coreColor })),
  );
  const halo = new Sprite(
    track(
      new SpriteMaterial({
        map: glow,
        color: coreColor,
        blending: AdditiveBlending,
        transparent: true,
        depthWrite: false,
      }),
    ),
  );
  halo.scale.setScalar(7);
  core.add(shell, heart, halo);
  world.add(core);

  // Orbit rings.
  const ringGroups = RINGS.map((ring) => {
    const group = new Group();
    group.rotation.x = Math.PI / 2 + ring.tilt;
    const points = new Float32Array(129 * 3);
    for (let i = 0; i <= 128; i++) {
      const a = (i / 128) * Math.PI * 2;
      points.set([Math.cos(a) * ring.radius, Math.sin(a) * ring.radius, 0], i * 3);
    }
    const geometry = track(new BufferGeometry());
    geometry.setAttribute('position', new BufferAttribute(points, 3));
    group.add(
      new LineLoop(
        geometry,
        track(new LineBasicMaterial({ color: 0x8aa4c8, transparent: true, opacity: 0.18 })),
      ),
    );
    world.add(group);
    return group;
  });

  interface Body {
    node: OrbitNode;
    ring: number;
    angle: number;
    mesh: Mesh;
    halo: Sprite;
    link: Line;
    particles: Points;
    offsets: number[];
    world: Vector3;
  }
  let bodies: Body[] = [];
  let selected: string | null = null;
  let coreActive = false;
  let motion = true;
  const sphere = track(new SphereGeometry(1, 24, 24));

  function remove(body: Body) {
    for (const object of [body.mesh, body.halo, body.link, body.particles]) {
      object.removeFromParent();
      const material = (object as { material?: Material }).material;
      material?.dispose();
    }
    body.link.geometry.dispose();
    body.particles.geometry.dispose();
  }

  function update(nodes: readonly OrbitNode[], active: boolean) {
    coreActive = active;
    const previous = new Map(bodies.map((x) => [x.node.id, x]));
    bodies = nodes.map((node, i) => {
      const color = new Color(node.color);
      const existing = previous.get(node.id);
      if (existing) {
        previous.delete(node.id);
        existing.node = node;
        (existing.mesh.material as MeshBasicMaterial).color = color;
        (existing.halo.material as SpriteMaterial).color = color;
        (existing.link.material as LineBasicMaterial).color = color;
        (existing.particles.material as PointsMaterial).color = color;
        return existing;
      }
      const ring = i % RINGS.length;
      const mesh = new Mesh(sphere, new MeshBasicMaterial({ color }));
      mesh.userData['id'] = node.id;
      const halo = new Sprite(
        new SpriteMaterial({
          map: glow,
          color,
          blending: AdditiveBlending,
          transparent: true,
          depthWrite: false,
        }),
      );
      const linkGeometry = new BufferGeometry();
      linkGeometry.setAttribute('position', new BufferAttribute(new Float32Array(6), 3));
      const link = new Line(
        linkGeometry,
        new LineBasicMaterial({ color, transparent: true, opacity: 0.22 }),
      );
      const particleGeometry = new BufferGeometry();
      particleGeometry.setAttribute(
        'position',
        new BufferAttribute(new Float32Array(PARTICLES * 3), 3),
      );
      const particles = new Points(
        particleGeometry,
        new PointsMaterial({
          size: 0.22,
          color,
          transparent: true,
          opacity: 0.95,
          blending: AdditiveBlending,
          depthWrite: false,
        }),
      );
      world.add(mesh, halo, link, particles);
      return {
        node,
        ring,
        // Spread each ring's nodes evenly, offset per ring so they do not line up.
        angle:
          (Math.floor(i / RINGS.length) / Math.ceil(nodes.length / RINGS.length)) * Math.PI * 2 +
          ring * 0.9,
        mesh,
        halo,
        link,
        particles,
        offsets: Array.from({ length: PARTICLES }, (_, k) => k / PARTICLES),
        world: new Vector3(),
      };
    });
    for (const body of previous.values()) remove(body);
    if (!motion) frame(performance.now());
  }

  // Pointer: parallax, and picking nodes with a click.
  const pointer = new Vector2(),
    lean = new Vector2();
  const raycaster = new Raycaster();
  const move = (event: PointerEvent) => {
    const box = canvas.getBoundingClientRect();
    pointer.set(
      ((event.clientX - box.left) / box.width) * 2 - 1,
      -((event.clientY - box.top) / box.height) * 2 + 1,
    );
  };
  const click = (event: PointerEvent) => {
    move(event);
    raycaster.setFromCamera(pointer, camera);
    const hit = raycaster.intersectObjects(bodies.map((x) => x.mesh))[0];
    if (hit) options.pick(String(hit.object.userData['id']));
  };
  canvas.addEventListener('pointermove', move, { passive: true });
  canvas.addEventListener('click', click as EventListener);

  let width = 0,
    height = 0;
  const resize = () => {
    const box = canvas.parentElement!.getBoundingClientRect();
    if (box.width === width && box.height === height) return;
    width = box.width;
    height = box.height;
    renderer.setSize(width, height, false);
    camera.aspect = width / Math.max(1, height);
    // Narrow views step back so the outer ring still fits.
    camera.position.z = width < 640 ? 30 : 22;
    camera.updateProjectionMatrix();
    if (!motion) frame(performance.now());
  };
  const observer = new ResizeObserver(resize);
  observer.observe(canvas.parentElement!);
  resize();

  let visible = true;
  const visibility = new IntersectionObserver(([entry]) => {
    visible = entry.isIntersecting;
    if (visible && motion) loop();
  });
  visibility.observe(canvas);

  const projected = new Vector3();
  const positions = new Map<string, { x: number; y: number; depth: number }>();
  let last = performance.now();
  let raf = 0;

  function frame(now: number) {
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    const t = now / 1000;
    if (motion) {
      world.rotation.y += dt * 0.04;
      lean.lerp(pointer, 0.04);
      world.rotation.x = lean.y * -0.12;
      world.rotation.z = lean.x * 0.05;
      stars.rotation.y += dt * 0.005;
      shell.rotation.y += dt * (coreActive ? 0.6 : 0.18);
      shell.rotation.x += dt * 0.07;
    }
    const pulse = coreActive && motion ? 1 + Math.sin(t * 3.2) * 0.08 : 1;
    heart.scale.setScalar(pulse);
    halo.scale.setScalar(7 * pulse);
    positions.clear();
    for (const body of bodies) {
      const ring = RINGS[body.ring];
      if (motion) body.angle += dt * ring.speed * (body.node.active ? 2.2 : 1);
      const local = new Vector3(
        Math.cos(body.angle) * ring.radius,
        Math.sin(body.angle) * ring.radius,
        0,
      );
      local.applyEuler(ringGroups[body.ring].rotation);
      body.mesh.position.copy(local);
      const size = 0.32 + body.node.weight * 0.55;
      const focus = selected === body.node.id ? 1.45 : 1;
      body.mesh.scale.setScalar(size * focus);
      body.halo.position.copy(local);
      body.halo.scale.setScalar(size * (body.node.active ? 7 + Math.sin(t * 4) : 5) * focus);
      const line = body.link.geometry.getAttribute('position') as BufferAttribute;
      line.setXYZ(1, local.x, local.y, local.z);
      line.needsUpdate = true;
      (body.link.material as LineBasicMaterial).opacity = selected === body.node.id ? 0.7 : 0.22;
      const dots = body.particles.geometry.getAttribute('position') as BufferAttribute;
      body.particles.visible = body.node.active;
      if (body.node.active)
        body.offsets.forEach((offset, k) => {
          const p = motion ? (offset + t * 0.35) % 1 : offset;
          dots.setXYZ(k, local.x * (1 - p), local.y * (1 - p), local.z * (1 - p));
        });
      dots.needsUpdate = true;
      body.mesh.getWorldPosition(body.world);
      projected.copy(body.world).project(camera);
      positions.set(body.node.id, {
        x: ((projected.x + 1) / 2) * width,
        y: ((1 - projected.y) / 2) * height,
        depth: projected.z,
      });
    }
    renderer.render(scene, camera);
    options.place(positions);
  }

  function loop() {
    cancelAnimationFrame(raf);
    const tick = (now: number) => {
      if (!visible || !motion || document.hidden) {
        raf = 0;
        return;
      }
      frame(now);
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
  }
  const resume = () => {
    if (!document.hidden && motion) {
      last = performance.now();
      loop();
    }
  };
  document.addEventListener('visibilitychange', resume);
  loop();

  return {
    update,
    select(id) {
      selected = id;
      if (!motion) frame(performance.now());
    },
    setMotion(enabled) {
      motion = enabled;
      if (enabled) resume();
      else {
        cancelAnimationFrame(raf);
        frame(performance.now());
      }
    },
    dispose() {
      cancelAnimationFrame(raf);
      observer.disconnect();
      visibility.disconnect();
      document.removeEventListener('visibilitychange', resume);
      canvas.removeEventListener('pointermove', move);
      canvas.removeEventListener('click', click as EventListener);
      for (const body of bodies) remove(body);
      for (const item of disposables) item.dispose();
      renderer.dispose();
    },
  };
}
