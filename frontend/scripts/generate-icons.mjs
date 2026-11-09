import { readFile, writeFile } from 'node:fs/promises';
import sharp from 'sharp';
import { fileURLToPath } from 'node:url';

const root = new URL('../src/app/', import.meta.url);
const mark = JSON.parse(await readFile(new URL('../src/lib/brand-mark.json', import.meta.url), 'utf8'));
const source = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="${mark.viewBox}" fill="${mark.color}"><path d="${mark.path}"/></svg>\n`);
await writeFile(new URL('icon.svg', root), source);
const sizes = [16, 32, 48];
const pngs = await Promise.all(sizes.map(size => sharp(source).resize(size, size).png().toBuffer()));
const header = Buffer.alloc(6 + sizes.length * 16);
header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
let offset = header.length;
pngs.forEach((png, i) => {
  const entry = 6 + i * 16;
  header[entry] = sizes[i]; header[entry + 1] = sizes[i];
  header.writeUInt16LE(1, entry + 4); header.writeUInt16LE(32, entry + 6);
  header.writeUInt32LE(png.length, entry + 8); header.writeUInt32LE(offset, entry + 12);
  offset += png.length;
});
await writeFile(new URL('favicon.ico', root), Buffer.concat([header, ...pngs]));
await sharp(source).resize(180, 180).png().toFile(fileURLToPath(new URL('apple-icon.png', root)));
console.log('Generated favicon.ico (16/32/48) and apple-icon.png (180) from icon.svg.');
