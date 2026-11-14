import type { components } from './api/schema';

type Schemas = components['schemas'];
export type Tone = 'ok' | 'warn' | 'danger' | 'muted' | 'accent';

export const xrayStates: Record<string, { label: string; tone: Tone }> = {
	NotInstalled: { label: 'не установлен', tone: 'muted' },
	Stopped: { label: 'остановлен', tone: 'muted' },
	Starting: { label: 'запускается', tone: 'accent' },
	Running: { label: 'работает', tone: 'ok' },
	Restarting: { label: 'перезапускается', tone: 'warn' },
	Failed: { label: 'ошибка', tone: 'danger' }
};

export const connectionKinds: Record<string, string> = {
	Socks: 'SOCKS5',
	Http: 'HTTP',
	Wireguard: 'WireGuard'
};

export const sourceKinds: Record<string, string> = {
	GitHubRepository: 'GitHub-репозиторий (формат v2fly)',
	GeositeDat: 'geosite.dat',
	GeoipDat: 'geoip.dat',
	Local: 'Локальные списки'
};

export const routeStates: Record<string, { label: string; tone: Tone }> = {
	Pending: { label: 'не применён', tone: 'warn' },
	Enabled: { label: 'включён', tone: 'ok' },
	Disabled: { label: 'выключен', tone: 'muted' }
};

export const updateProxyStates: Record<string, { label: string; tone: Tone }> = {
	Disabled: { label: 'выключен', tone: 'muted' },
	Idle: { label: 'в ожидании', tone: 'accent' },
	Running: { label: 'работает', tone: 'ok' },
	Unavailable: { label: 'недоступен', tone: 'warn' }
};

export const refreshStatuses: Record<string, { label: string; tone: Tone }> = {
	Ok: { label: 'успешно', tone: 'ok' },
	Partial: { label: 'частично', tone: 'warn' },
	Waiting: { label: 'ожидает источник', tone: 'warn' },
	Failed: { label: 'ошибка', tone: 'danger' }
};

/** Варианты интервала обновления в выпадающих списках, часы. */
export const intervalChoices = [1, 3, 6, 12, 24] as const;

/** Варианты для списка: стандартные плюс текущее значение, если оно задано вне списка (например, через API). */
export function intervalOptions(current: string): number[] {
	const value = Number(current);
	const choices: number[] = [...intervalChoices];
	return current && !choices.includes(value) ? [...choices, value].sort((a, b) => a - b) : choices;
}

/** «1 час», «3 часа», «12 часов». */
export function hoursLabel(hours: number): string {
	const mod10 = hours % 10;
	const mod100 = hours % 100;
	const word =
		mod10 === 1 && mod100 !== 11
			? 'час'
			: mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)
				? 'часа'
				: 'часов';
	return `${hours} ${word}`;
}

export function label<T extends { label: string; tone: Tone }>(
	map: Record<string, T>,
	key: string | null | undefined
): { label: string; tone: Tone } {
	return (key && map[key]) || { label: key ?? '—', tone: 'muted' };
}

/** Состояние интерфейса Keenetic соединения одной строкой. */
export function interfaceState(connection: Schemas['ConnectionDto']): {
	label: string;
	tone: Tone;
} {
	const iface = connection.interface;
	if (!iface || !iface.exists) return { label: 'нет интерфейса', tone: 'danger' };
	if (iface.state === 'down') return { label: 'выключен', tone: 'muted' };
	if (connection.kind === 'Wireguard') {
		return iface.wireguardOnline
			? { label: 'активен', tone: 'ok' }
			: { label: 'нет рукопожатия', tone: 'warn' };
	}
	return iface.connected
		? { label: 'подключён', tone: 'ok' }
		: { label: 'не подключён', tone: 'warn' };
}
