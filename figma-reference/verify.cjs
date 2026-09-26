const fs = require('node:fs');
const path = require('node:path');

const project = path.resolve(__dirname, '..', 'CampusGear', 'CampusGear');
const catalog = JSON.parse(fs.readFileSync(path.join(project, 'wwwroot', 'figma', 'catalog.json'), 'utf8'));
const assets = path.join(project, 'wwwroot', 'figma', 'assets');
const views = path.join(project, 'Pages', 'Figma', 'Frames');
const failures = [];
const allImages = new Set();

if (catalog.length !== 70) failures.push(`Expected 70 frames, found ${catalog.length}`);
if (new Set(catalog.map(frame => frame.id)).size !== catalog.length) failures.push('Duplicate Figma node ID');
if (new Set(catalog.map(frame => frame.url)).size !== catalog.length) failures.push('Duplicate route URL');
const demoPage = fs.readFileSync(path.join(project, 'Pages', 'Screen.cshtml'), 'utf8');
if (!demoPage.includes('@page "/demo/{workspace}/{screen}"')) failures.push('Figma screen route is not isolated under /demo');

for (const frame of catalog) {
  const expectedUrl = `/demo/${frame.route}${frame.state === 'default' ? '' : `?state=${frame.state}`}`;
  if (frame.url !== expectedUrl) failures.push(`${frame.id}: expected ${expectedUrl}, found ${frame.url}`);
  const viewPath = path.join(views, frame.viewName);
  if (!fs.existsSync(viewPath)) { failures.push(`${frame.id}: missing Razor partial`); continue; }
  const html = fs.readFileSync(viewPath, 'utf8');
  if (!html.includes(`data-node-id="${frame.id}"`)) failures.push(`${frame.id}: missing source node ID`);
  if (html.includes('https://www.figma.com/api/mcp/asset/')) failures.push(`${frame.id}: temporary asset URL`);
  for (const [, name] of html.matchAll(/src="\/figma\/assets\/([^"]+)"/g)) {
    allImages.add(name);
    const file = path.join(assets, name);
    if (!fs.existsSync(file) || fs.statSync(file).size === 0) failures.push(`${frame.id}: missing/empty ${name}`);
  }
}

for (const name of ['inter-latin-400-normal.woff2', 'inter-latin-500-normal.woff2', 'inter-latin-600-normal.woff2', 'inter-latin-700-normal.woff2', 'oxanium-latin-500-normal.woff2', 'oxanium-latin-700-normal.woff2', 'sora-latin-600-normal.woff2', 'space-grotesk-latin-400-normal.woff2', 'space-grotesk-latin-500-normal.woff2', 'space-grotesk-latin-700-normal.woff2']) {
  const file = path.join(project, 'wwwroot', 'figma', 'fonts', name);
  if (!fs.existsSync(file) || fs.statSync(file).size === 0) failures.push(`Missing font ${name}`);
}

console.log(`${catalog.length} frames, ${allImages.size} referenced local image files, ${failures.length} failures.`);
if (failures.length) { console.error(failures.join('\n')); process.exitCode = 1; }
