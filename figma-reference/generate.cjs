// Converts the exact JSX returned for each Figma frame into static Razor partials.
// The conversion tool runs outside the ASP.NET application; no React/Tailwind
// package is used by the application at build time or runtime.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');

const root = path.resolve(__dirname, '..');
const sourceDir = path.join(__dirname, 'generated');
const viewDir = path.join(root, 'CampusGear', 'CampusGear.WebApp', 'Pages', 'Figma', 'Frames');
const assetDir = path.join(root, 'CampusGear', 'CampusGear.WebApp', 'wwwroot', 'figma', 'assets');
const toolDir = process.env.FIGMA_TOOLS_DIR || path.join(process.env.TEMP || '', 'campusgear-figma-tools');
const esbuild = require(path.join(toolDir, 'node_modules', 'esbuild'));
const frames = JSON.parse(fs.readFileSync(path.join(__dirname, 'frames.json'), 'utf8')).frames;

function escapeHtml(value) {
  return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}

function h(type, props, ...children) {
  const all = [...(props && props.children !== undefined ? [props.children] : []), ...children];
  if (typeof type === 'function') return type({ ...(props || {}), children: all });
  return { type, props: props || {}, children: all };
}

function render(node) {
  if (node == null || typeof node === 'boolean') return '';
  if (Array.isArray(node)) return node.map(render).join('');
  if (typeof node !== 'object') return escapeHtml(node);
  const { type, props, children } = node;
  if (type === 'Fragment') return render(children);
  const attrs = Object.entries(props).filter(([key, value]) => key !== 'children' && key !== 'key' && value !== false && value != null).map(([key, value]) => {
    const name = key === 'className' ? 'class' : key === 'htmlFor' ? 'for' : key;
    if (value === true) return ` ${name}`;
    if (name === 'style' && typeof value === 'object') {
      value = Object.entries(value).map(([k, v]) => `${k.replace(/[A-Z]/g, m => '-' + m.toLowerCase())}:${v}`).join(';');
    }
    return ` ${name}="${escapeHtml(value)}"`;
  }).join('');
  if (['img', 'input', 'br', 'hr', 'meta', 'link'].includes(type)) return `<${type}${attrs}>`;
  return `<${type}${attrs}>${render(children)}</${type}>`;
}

function sourceFor(id) {
  return fs.readFileSync(path.join(sourceDir, `${id.replace(':', '_')}.tsx.txt`), 'utf8');
}

function assetsFor(code) {
  const prefix = code.match(/const assetPathPrefix = "([^"]+)";/)?.[1];
  if (!prefix) return [];
  return [...code.matchAll(/const\s+(\w+)\s*=\s*`\$\{assetPathPrefix\}\/([^`]+)`;/g)].map(m => ({ symbol: m[1], name: m[2], url: `${prefix}/${m[2]}` }));
}

function generateViews() {
  fs.mkdirSync(viewDir, { recursive: true });
  const manifest = [];
  const assets = new Map();
  for (const frame of frames) {
    const code = sourceFor(frame.id);
    for (const asset of assetsFor(code)) {
      if (!assets.has(asset.name)) assets.set(asset.name, asset.url);
    }
    const js = esbuild.transformSync(code, { loader: 'tsx', format: 'cjs', jsxFactory: 'h', jsxFragment: 'Fragment', target: 'es2022' }).code;
    const exports = {};
    const module = { exports };
    const context = { h, Fragment: 'Fragment', exports, module, CodeConnectSnippet: p => p.children, Lock: () => h('img', { className: 'figma-lock-icon', src: '/figma/assets/lock.png', alt: '', 'aria-hidden': 'true' }) };
    vm.runInNewContext(js, context, { timeout: 2500, filename: frame.id });
    const component = module.exports.default;
    if (typeof component !== 'function') throw new Error(`No component for ${frame.id}`);
    let html = render(component());
    for (const asset of assetsFor(code)) html = html.replaceAll(asset.url, `/figma/assets/${asset.name}`);
    if (html.includes('https://www.figma.com/api/mcp/asset/')) throw new Error(`Temporary asset URL remains in ${frame.id}`);
    // Razor treats @ as code; an HTML entity preserves Figma's visible text.
    html = html.replaceAll('@', '&#64;');
    const viewName = `_${frame.id.replace(':', '_')}.cshtml`;
    fs.writeFileSync(path.join(viewDir, viewName), `@* Source Figma node ${frame.id}: ${frame.name.replaceAll('*', '')} *@\n${html}\n`);
    manifest.push({ ...frame, viewName, assetNames: assetsFor(code).map(a => a.name) });
  }
  fs.writeFileSync(path.join(__dirname, 'render-manifest.json'), JSON.stringify({ frames: manifest, assets: [...assets].map(([name, url]) => ({ name, url })) }, null, 2));
  const groups = JSON.parse(fs.readFileSync(path.join(__dirname, 'route-groups.json'), 'utf8'));
  const catalog = [];
  for (const group of groups) {
    for (const spec of group.frames) {
      const [indexText, state] = spec.split(':');
      const frame = manifest[Number(indexText)];
      if (!frame || !state) throw new Error(`Invalid route entry ${spec}`);
      catalog.push({ id: frame.id, title: frame.name, section: frame.section, route: group.route, state, url: `/demo/${group.route}${state === 'default' ? '' : `?state=${state}`}`, viewName: frame.viewName });
    }
  }
  if (catalog.length !== frames.length || new Set(catalog.map(x => x.id)).size !== frames.length) throw new Error('Route catalog does not cover every Figma frame once');
  const catalogDir = path.join(root, 'CampusGear', 'CampusGear.WebApp', 'wwwroot', 'figma');
  fs.mkdirSync(catalogDir, { recursive: true });
  fs.writeFileSync(path.join(catalogDir, 'catalog.json'), JSON.stringify(catalog, null, 2));
  console.log(`Generated ${manifest.length} Razor frame partials and indexed ${assets.size} unique assets.`);
}

async function downloadAssets() {
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, 'render-manifest.json'), 'utf8'));
  fs.mkdirSync(assetDir, { recursive: true });
  const queue = manifest.assets.filter(a => !fs.existsSync(path.join(assetDir, a.name)) || fs.statSync(path.join(assetDir, a.name)).size === 0);
  let next = 0, done = 0;
  async function worker() {
    while (next < queue.length) {
      const asset = queue[next++];
      const response = await fetch(asset.url);
      if (!response.ok) throw new Error(`${asset.name}: HTTP ${response.status}`);
      const bytes = Buffer.from(await response.arrayBuffer());
      if (!bytes.length) throw new Error(`${asset.name}: empty download`);
      fs.writeFileSync(path.join(assetDir, asset.name), bytes);
      done++;
      if (done % 25 === 0) console.log(`Downloaded ${done}/${queue.length}`);
    }
  }
  await Promise.all(Array.from({ length: Math.min(10, queue.length) }, worker));
  const checks = manifest.assets.map(a => ({ name: a.name, bytes: fs.statSync(path.join(assetDir, a.name)).size, sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(assetDir, a.name))).digest('hex') }));
  fs.writeFileSync(path.join(__dirname, 'asset-checksums.json'), JSON.stringify(checks, null, 2));
  console.log(`Verified ${checks.length} nonempty local assets.`);
}

const mode = process.argv[2] || 'views';
if (mode === 'views') generateViews();
else if (mode === 'assets') downloadAssets().catch(e => { console.error(e); process.exitCode = 1; });
else throw new Error(`Unknown mode ${mode}`);
