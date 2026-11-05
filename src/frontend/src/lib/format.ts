/** Числа из API могут прийти строкой (OpenAPI .NET: integer | string). */
export function toNumber(value: number | string | null | undefined): number | undefined {
	if (value === null || value === undefined || value === '') return undefined;
	const number = typeof value === 'number' ? value : Number(value);
	return Number.isFinite(number) ? number : undefined;
}

/** Килобайты → «12,3 МиБ». */
export function formatKilobytes(value: number | string | null | undefined): string {
	const kb = toNumber(value);
	if (kb === undefined) return '—';
	if (kb < 1024) return `${kb} КиБ`;
	return `${(kb / 1024).toLocaleString('ru-RU', { maximumFractionDigits: 1 })} МиБ`;
}

/** Секунды → «2 д 3 ч», «5 ч 12 мин», «40 с». */
export function formatDuration(value: number | string | null | undefined): string {
	const total = toNumber(value);
	if (total === undefined) return '—';
	const days = Math.floor(total / 86400);
	const hours = Math.floor((total % 86400) / 3600);
	const minutes = Math.floor((total % 3600) / 60);
	if (days > 0) return `${days} д ${hours} ч`;
	if (hours > 0) return `${hours} ч ${minutes} мин`;
	if (minutes > 0) return `${minutes} мин`;
	return `${Math.floor(total)} с`;
}

/** ISO-дата → локальное время. */
export function formatDateTime(value: string | null | undefined): string {
	if (!value) return '—';
	const date = new Date(value);
	return Number.isNaN(date.getTime()) ? value : date.toLocaleString('ru-RU');
}

/** Байты → «1,5 ГиБ». */
export function formatBytes(value: number | string | null | undefined): string {
	const bytes = toNumber(value);
	if (bytes === undefined) return '—';
	const units = ['Б', 'КиБ', 'МиБ', 'ГиБ', 'ТиБ'];
	let size = bytes;
	let unit = 0;
	while (size >= 1024 && unit < units.length - 1) {
		size /= 1024;
		unit++;
	}
	return `${size.toLocaleString('ru-RU', { maximumFractionDigits: unit === 0 ? 0 : 1 })} ${units[unit]}`;
}

/** Хост из адреса без пути и токена — для показа ссылок подписок. */
export function hostOf(url: string): string {
	try {
		return new URL(url).host;
	} catch {
		return url.slice(0, 32);
	}
}
