const fs = require('node:fs');
const path = require('node:path');
const toolDir = process.env.FIGMA_TOOLS_DIR || path.join(process.env.TEMP || '', 'campusgear-figma-tools');
const cheerio = require(path.join(toolDir, 'node_modules', 'cheerio'));
const root = path.resolve(__dirname, '..');
const catalog = JSON.parse(fs.readFileSync(path.join(root, 'CampusGear', 'CampusGear', 'wwwroot', 'figma', 'catalog.json'), 'utf8'));
for (const screen of catalog) {
  if (screen.state !== 'default') continue;
  const html = fs.readFileSync(path.join(root, 'CampusGear', 'CampusGear', 'Pages', 'Figma', 'Frames', screen.viewName), 'utf8');
  const $ = cheerio.load(html);
  const links = $('a').map((_, a) => ({ name: $(a).attr('data-name') || '', text: $(a).text().trim().replace(/\s+/g, ' ') })).get();
  console.log(`\n${screen.route} (${screen.id}): ${links.length} anchors`);
  console.log([...new Set(links.map(x => `${x.name || '(unnamed)'} ${x.text ? `=> ${x.text}` : ''}`))].join('\n'));
}
