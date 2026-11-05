// nginx из Entware собран без brotli: оставляем только .gz для gzip_static.
import { readdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';

const root = process.argv[2] ?? 'dist';
let removed = 0;

for (const entry of readdirSync(root, { recursive: true, withFileTypes: true })) {
	if (entry.isFile() && entry.name.endsWith('.br')) {
		rmSync(join(entry.parentPath, entry.name));
		removed++;
	}
}

console.log(`strip-brotli: удалено ${removed} файлов .br из ${root}`);
