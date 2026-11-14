import type { IconName } from '#lib/components/Icon.svelte';

export type NavPath =
	| '/'
	| '/xray'
	| '/servers'
	| '/subscriptions'
	| '/connections'
	| '/routes'
	| '/sources'
	| '/settings';

export interface NavLink {
	path: NavPath;
	title: string;
	icon: IconName;
	/** Слова для поиска в палитре команд. */
	keywords?: string;
}

export interface NavGroup {
	id: 'xray' | 'routing';
	title: string;
	icon: IconName;
	links: NavLink[];
}

export const overviewLink: NavLink = {
	path: '/',
	title: 'Обзор',
	icon: 'overview',
	keywords: 'дашборд главная'
};

export const settingsLink: NavLink = {
	path: '/settings',
	title: 'Настройки',
	icon: 'settings',
	keywords: 'прокси интервалы логи'
};

export const navGroups: NavGroup[] = [
	{
		id: 'xray',
		title: 'Xray',
		icon: 'xray',
		links: [
			{ path: '/xray', title: 'Ядро', icon: 'core', keywords: 'xray версия журнал логи' },
			{ path: '/servers', title: 'Серверы', icon: 'server', keywords: 'outbound vless' },
			{ path: '/subscriptions', title: 'Подписки', icon: 'subscription', keywords: 'провайдер' },
			{
				path: '/connections',
				title: 'Соединения',
				icon: 'plug',
				keywords: 'интерфейс wireguard socks'
			}
		]
	},
	{
		id: 'routing',
		title: 'Маршруты',
		icon: 'route',
		links: [
			{ path: '/routes', title: 'DNS-маршруты', icon: 'route', keywords: 'правила группы' },
			{ path: '/sources', title: 'Источники списков', icon: 'layers', keywords: 'домены geosite' }
		]
	}
];

export const allLinks: NavLink[] = [
	overviewLink,
	...navGroups.flatMap((group) => group.links),
	settingsLink
];

/** Ссылка активна: точное совпадение для «/», иначе по префиксу. */
export function isActive(path: NavPath, pathname: string): boolean {
	return path === '/' ? pathname === '/' : pathname === path || pathname.startsWith(`${path}/`);
}

/** Текущий раздел для заголовка мобильной полосы. */
export function currentLink(pathname: string): NavLink | undefined {
	return (
		allLinks.find((link) => link.path !== '/' && isActive(link.path, pathname)) ??
		(pathname === '/' ? overviewLink : undefined)
	);
}
