/** Встроенный источник v2fly domain-list-community: есть с первого запуска, файл качается по запросу. */
export const geositeSourceId = 'v2fly';

/** Список, по которому мастер создаёт маршрут. */
export interface SetupList {
	sourceId: string;
	name: string;
}

/** Плитка мастера: сервис и списки, которые его покрывают. */
export interface Preset {
	id: string;
	title: string;
	lists: SetupList[];
}

const geosite = (name: string): SetupList => ({ sourceId: geositeSourceId, name });

/** Популярные сервисы по спискам v2fly domain-list-community. */
export const presets: Preset[] = [
	{ id: 'anthropic', title: 'Claude', lists: [geosite('anthropic')] },
	{ id: 'openai', title: 'ChatGPT', lists: [geosite('openai')] },
	{ id: 'gemini', title: 'Gemini', lists: [geosite('google-gemini')] },
	{ id: 'spotify', title: 'Spotify', lists: [geosite('spotify')] }
];

/** Быстрые подсказки под поиском: имена списков v2fly, которые чаще всего ищут. */
export const suggestions = [
	'youtube',
	'telegram',
	'instagram',
	'facebook',
	'whatsapp',
	'twitter',
	'discord',
	'netflix',
	'tiktok',
	'twitch',
	'linkedin',
	'notion'
];

/** Ключ списка для сравнения и хранения. */
export function listKey(list: SetupList): string {
	return `${list.sourceId}:${list.name}`;
}
