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
	Pending: { label: 'не создан', tone: 'warn' },
	Enabled: { label: 'включён', tone: 'ok' },
	Disabled: { label: 'выключен', tone: 'muted' }
};

export const refreshStatuses: Record<string, { label: string; tone: Tone }> = {
	Ok: { label: 'ок', tone: 'ok' },
	Partial: { label: 'частично', tone: 'warn' },
	Waiting: { label: 'ждёт источник', tone: 'warn' },
	Failed: { label: 'ошибка', tone: 'danger' }
};

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
			? { label: 'на связи', tone: 'ok' }
			: { label: 'нет рукопожатия', tone: 'warn' };
	}
	return iface.connected
		? { label: 'подключён', tone: 'ok' }
		: { label: 'не подключён', tone: 'warn' };
}
