import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { deflateSync, inflateSync } from 'node:zlib';

const sourcePath = 'C:\\Users\\HP\\AppData\\Roaming\\Cursor\\User\\workspaceStorage\\bcb94f0d9b7ffa4e7c0c343ddbd30a98\\images\\a_sleek_app_icon_style_logo_on_a_dark_green_black-a3a28185-58ec-4bab-9988-9fbe9b6b2ce4.png';
const root = resolve('assets');
const forest = [0x06, 0x13, 0x0d, 255];

const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n += 1) {
  let c = n;
  for (let k = 0; k < 8; k += 1) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
  crcTable[n] = c >>> 0;
}

function crc32(buffer) {
  let c = 0xffffffff;
  for (const byte of buffer) c = crcTable[(c ^ byte) & 255] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);
  const name = Buffer.from(type);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([name, data])), 0);
  return Buffer.concat([length, name, data, crc]);
}

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}

function decodePng(buffer) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (!buffer.subarray(0, 8).equals(signature)) throw new Error('Source is not a PNG');
  let offset = 8;
  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = 0;
  const idat = [];
  while (offset < buffer.length) {
    const length = buffer.readUInt32BE(offset);
    offset += 4;
    const type = buffer.toString('ascii', offset, offset + 4);
    offset += 4;
    const data = buffer.subarray(offset, offset + length);
    offset += length + 4;
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
    } else if (type === 'IDAT') {
      idat.push(data);
    } else if (type === 'IEND') {
      break;
    }
  }
  if (bitDepth !== 8 || (colorType !== 2 && colorType !== 6)) {
    throw new Error(`Unsupported PNG ${width}x${height} depth ${bitDepth} color ${colorType}`);
  }
  const channels = colorType === 6 ? 4 : 3;
  const inflated = inflateSync(Buffer.concat(idat));
  const stride = width * channels;
  const rgba = Buffer.alloc(width * height * 4);
  let input = 0;
  let previous = Buffer.alloc(stride);
  for (let y = 0; y < height; y += 1) {
    const filter = inflated[input];
    input += 1;
    const row = Buffer.alloc(stride);
    for (let x = 0; x < stride; x += 1) {
      const raw = inflated[input];
      input += 1;
      const left = x >= channels ? row[x - channels] : 0;
      const up = previous[x];
      const upLeft = x >= channels ? previous[x - channels] : 0;
      let value = raw;
      if (filter === 1) value = (raw + left) & 255;
      else if (filter === 2) value = (raw + up) & 255;
      else if (filter === 3) value = (raw + Math.floor((left + up) / 2)) & 255;
      else if (filter === 4) value = (raw + paeth(left, up, upLeft)) & 255;
      else if (filter !== 0) throw new Error(`Unknown PNG filter ${filter}`);
      row[x] = value;
    }
    previous = row;
    for (let x = 0; x < width; x += 1) {
      const target = (y * width + x) * 4;
      const source = x * channels;
      rgba[target] = row[source];
      rgba[target + 1] = row[source + 1];
      rgba[target + 2] = row[source + 2];
      rgba[target + 3] = channels === 4 ? row[source + 3] : 255;
    }
  }
  return { width, height, rgba };
}

function encodePng(width, height, rgba) {
  const raw = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y += 1) {
    const start = y * (width * 4 + 1);
    raw[start] = 0;
    rgba.copy(raw, start + 1, y * width * 4, (y + 1) * width * 4);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  return Buffer.concat([
    signature,
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

function pixel(image, x, y) {
  const index = (y * image.width + x) * 4;
  return [image.rgba[index], image.rgba[index + 1], image.rgba[index + 2], image.rgba[index + 3]];
}

function markAlpha(r, g, b, a) {
  if (a < 16) return 0;
  const max = Math.max(r, g, b);
  const greenLead = g - b;
  if (max > 210 && Math.abs(r - g) < 22 && Math.abs(g - b) < 22) return 0;
  if (b > g + 8 && b > r) return 0;
  if (greenLead < 14 && max < 150) return 0;
  const chroma = Math.max(0, Math.min(1, (greenLead - 8) / 36));
  const brightness = Math.max(0, Math.min(1, (max - 32) / 110));
  return Math.round(255 * Math.min(1, chroma * 0.82 + brightness * 0.45));
}

function isCore(r, g, b, a) {
  if (a < 16) return false;
  const max = Math.max(r, g, b);
  return g > 165 && g > b + 24 && max > 180 && g + 20 > r;
}

function largestComponent(mask, width, height) {
  const labels = new Int32Array(width * height);
  let bestCount = 0;
  let bestLabel = 0;
  let label = 0;
  for (let start = 0; start < mask.length; start += 1) {
    if (!mask[start] || labels[start]) continue;
    label += 1;
    const stack = [start];
    labels[start] = label;
    let count = 0;
    while (stack.length > 0) {
      const index = stack.pop();
      count += 1;
      const x = index % width;
      const y = (index - x) / width;
      const neighbors = [];
      if (x > 0) neighbors.push(index - 1);
      if (x + 1 < width) neighbors.push(index + 1);
      if (y > 0) neighbors.push(index - width);
      if (y + 1 < height) neighbors.push(index + width);
      for (const next of neighbors) {
        if (mask[next] && !labels[next]) {
          labels[next] = label;
          stack.push(next);
        }
      }
    }
    if (count > bestCount) {
      bestCount = count;
      bestLabel = label;
    }
  }
  const out = new Uint8Array(width * height);
  for (let index = 0; index < out.length; index += 1) {
    if (labels[index] === bestLabel) out[index] = 1;
  }
  return out;
}

function dilate(mask, width, height, radius) {
  const dist = new Float32Array(width * height);
  dist.fill(radius + 1);
  for (let index = 0; index < mask.length; index += 1) {
    if (mask[index]) dist[index] = 0;
  }
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const index = y * width + x;
      if (x > 0) dist[index] = Math.min(dist[index], dist[index - 1] + 1);
      if (y > 0) dist[index] = Math.min(dist[index], dist[index - width] + 1);
    }
  }
  for (let y = height - 1; y >= 0; y -= 1) {
    for (let x = width - 1; x >= 0; x -= 1) {
      const index = y * width + x;
      if (x + 1 < width) dist[index] = Math.min(dist[index], dist[index + 1] + 1);
      if (y + 1 < height) dist[index] = Math.min(dist[index], dist[index + width] + 1);
    }
  }
  const grown = new Uint8Array(width * height);
  for (let index = 0; index < grown.length; index += 1) grown[index] = dist[index] <= radius ? 1 : 0;
  return grown;
}

function extractMark(image) {
  let wordmarkTop = image.height;
  for (let y = Math.floor(image.height * 0.52); y < image.height; y += 1) {
    let white = 0;
    for (let x = 0; x < image.width; x += 1) {
      const [r, g, b] = pixel(image, x, y);
      if (r > 185 && g > 185 && b > 185) white += 1;
    }
    if (white > image.width * 0.015) {
      wordmarkTop = y;
      break;
    }
  }
  const limit = Math.max(1, wordmarkTop - Math.round(image.height * 0.04));
  const core = new Uint8Array(image.width * limit);
  for (let y = 0; y < limit; y += 1) {
    for (let x = 0; x < image.width; x += 1) {
      const [r, g, b, a] = pixel(image, x, y);
      if (isCore(r, g, b, a)) core[y * image.width + x] = 1;
    }
  }
  const symbol = largestComponent(core, image.width, limit);
  const grown = dilate(symbol, image.width, limit, Math.round(image.width * 0.02));
  let minX = image.width;
  let minY = limit;
  let maxX = 0;
  let maxY = 0;
  for (let y = 0; y < limit; y += 1) {
    for (let x = 0; x < image.width; x += 1) {
      if (!grown[y * image.width + x]) continue;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }
  if (maxX <= minX || maxY <= minY) throw new Error('Could not locate the G symbol');
  const width = maxX - minX + 1;
  const height = maxY - minY + 1;
  const rgba = Buffer.alloc(width * height * 4);
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const sourceX = minX + x;
      const sourceY = minY + y;
      const [r, g, b, a] = pixel(image, sourceX, sourceY);
      const target = (y * width + x) * 4;
      rgba[target] = r;
      rgba[target + 1] = g;
      rgba[target + 2] = b;
      rgba[target + 3] = grown[sourceY * image.width + sourceX] ? markAlpha(r, g, b, a) : 0;
    }
  }
  return { width, height, rgba, wordmarkTop };
}

function clearOuterBlack(image) {
  const { width, height, rgba } = image;
  const seen = new Uint8Array(width * height);
  const stack = [];
  const push = (x, y) => {
    if (x < 0 || y < 0 || x >= width || y >= height) return;
    const id = y * width + x;
    if (seen[id]) return;
    const index = id * 4;
    if (Math.max(rgba[index], rgba[index + 1], rgba[index + 2]) > 14) return;
    seen[id] = 1;
    rgba[index + 3] = 0;
    stack.push(x, y);
  };
  for (let x = 0; x < width; x += 1) {
    push(x, 0);
    push(x, height - 1);
  }
  for (let y = 0; y < height; y += 1) {
    push(0, y);
    push(width - 1, y);
  }
  while (stack.length > 0) {
    const y = stack.pop();
    const x = stack.pop();
    push(x - 1, y);
    push(x + 1, y);
    push(x, y - 1);
    push(x, y + 1);
  }
}

function sample(image, x, y) {
  const x0 = Math.max(0, Math.min(image.width - 1, Math.floor(x)));
  const y0 = Math.max(0, Math.min(image.height - 1, Math.floor(y)));
  const x1 = Math.min(image.width - 1, x0 + 1);
  const y1 = Math.min(image.height - 1, y0 + 1);
  const tx = Math.min(1, Math.max(0, x - x0));
  const ty = Math.min(1, Math.max(0, y - y0));
  const at = (px, py) => (py * image.width + px) * 4;
  const i00 = at(x0, y0);
  const i10 = at(x1, y0);
  const i01 = at(x0, y1);
  const i11 = at(x1, y1);
  const mix = (channel) => {
    const top = image.rgba[i00 + channel] * (1 - tx) + image.rgba[i10 + channel] * tx;
    const bottom = image.rgba[i01 + channel] * (1 - tx) + image.rgba[i11 + channel] * tx;
    return Math.round(top * (1 - ty) + bottom * ty);
  };
  return [mix(0), mix(1), mix(2), mix(3)];
}

function canvas(size, fill) {
  const rgba = Buffer.alloc(size * size * 4);
  if (fill) {
    for (let i = 0; i < rgba.length; i += 4) {
      rgba[i] = fill[0];
      rgba[i + 1] = fill[1];
      rgba[i + 2] = fill[2];
      rgba[i + 3] = fill[3];
    }
  }
  return { width: size, height: size, rgba };
}

function blit(mark, destination, occupancy, tint) {
  const scale = (destination.width * occupancy) / Math.max(mark.width, mark.height);
  const drawW = mark.width * scale;
  const drawH = mark.height * scale;
  const originX = (destination.width - drawW) / 2;
  const originY = (destination.height - drawH) / 2;
  for (let y = Math.floor(originY); y < Math.ceil(originY + drawH); y += 1) {
    for (let x = Math.floor(originX); x < Math.ceil(originX + drawW); x += 1) {
      if (x < 0 || y < 0 || x >= destination.width || y >= destination.height) continue;
      const [r, g, b, a] = sample(mark, (x - originX) / scale, (y - originY) / scale);
      if (a === 0) continue;
      const index = (y * destination.width + x) * 4;
      const alpha = a / 255;
      const srcR = tint ? 255 : r;
      const srcG = tint ? 255 : g;
      const srcB = tint ? 255 : b;
      const dstA = destination.rgba[index + 3] / 255;
      const outA = alpha + dstA * (1 - alpha);
      destination.rgba[index] = Math.round((srcR * alpha + destination.rgba[index] * dstA * (1 - alpha)) / outA);
      destination.rgba[index + 1] = Math.round((srcG * alpha + destination.rgba[index + 1] * dstA * (1 - alpha)) / outA);
      destination.rgba[index + 2] = Math.round((srcB * alpha + destination.rgba[index + 2] * dstA * (1 - alpha)) / outA);
      destination.rgba[index + 3] = Math.round(outA * 255);
    }
  }
}

function write(relative, image) {
  const path = resolve(root, relative);
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, encodePng(image.width, image.height, image.rgba));
  return path;
}

const source = decodePng(readFileSync(sourcePath));
const mark = extractMark(source);
mkdirSync(resolve(root, 'images'), { recursive: true });
const logo = {
  width: source.width,
  height: source.height,
  rgba: Buffer.from(source.rgba),
};
clearOuterBlack(logo);
write('images/growfit-logo.png', logo);

const icon = canvas(1024, forest);
blit(mark, icon, 0.62, false);
write('icon.png', icon);

const foreground = canvas(1024, null);
blit(mark, foreground, 0.56, false);
write('android-icon-foreground.png', foreground);

write('android-icon-background.png', canvas(1024, forest));

const monochrome = canvas(1024, null);
blit(mark, monochrome, 0.56, true);
write('android-icon-monochrome.png', monochrome);

const splash = canvas(1024, null);
blit(mark, splash, 0.86, false);
write('splash-icon.png', splash);

const opaque = icon.rgba.filter((_, index) => index % 4 === 3).every((alpha) => alpha === 255);
const transparentForeground = foreground.rgba.some((value, index) => index % 4 === 3 && value === 0);
console.log(JSON.stringify({
  source: { width: source.width, height: source.height },
  mark: { width: mark.width, height: mark.height, wordmarkTop: mark.wordmarkTop },
  iconOpaque: opaque,
  foregroundHasTransparency: transparentForeground,
}));
