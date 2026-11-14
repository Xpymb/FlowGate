/** Типы соединений: как их называет пользователь и чем они отличаются. */
export const connectionKindOptions = [
	{
		value: 'Wireguard',
		title: 'WireGuard',
		text: 'Поддержка TCP и UDP (QUIC, VoIP). Рекомендуется.'
	},
	{ value: 'Socks', title: 'SOCKS5', text: 'Интерфейс Proxy, только TCP.' },
	{ value: 'Http', title: 'HTTP', text: 'Интерфейс Proxy, только TCP.' }
] as const;

const kindSuffixes: Record<string, string> = { Wireguard: 'wg', Socks: 'socks', Http: 'http' };

/** Свободное имя вида xray-wg, xray-wg-2, … по типу соединения. */
export function suggestConnectionName(kind: string, taken: readonly string[]): string {
	const base = `xray-${kindSuffixes[kind] ?? kind.toLowerCase()}`;
	const used = new Set(taken.map((name) => name.toLowerCase()));
	if (!used.has(base)) return base;
	let index = 2;
	while (used.has(`${base}-${index}`)) index++;
	return `${base}-${index}`;
}
