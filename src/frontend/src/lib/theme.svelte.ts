export type ThemeMode = 'system' | 'light' | 'dark';

const storageKey = 'flowgate-theme';

function readMode(): ThemeMode {
	try {
		const value = localStorage.getItem(storageKey);
		return value === 'light' || value === 'dark' ? value : 'system';
	} catch {
		return 'system';
	}
}

const media =
	typeof window === 'undefined' ? undefined : window.matchMedia('(prefers-color-scheme: dark)');

/** Тема интерфейса: выбор пользователя и итоговая тёмность с учётом системы. */
export const theme = $state({
	mode: readMode(),
	systemDark: media?.matches ?? false,
	get dark(): boolean {
		return this.mode === 'dark' || (this.mode === 'system' && this.systemDark);
	}
});

media?.addEventListener('change', (event) => {
	theme.systemDark = event.matches;
});

/** Переключает тему по кругу: система → светлая → тёмная. */
export function cycleTheme(): void {
	const order: ThemeMode[] = ['system', 'light', 'dark'];
	setTheme(order[(order.indexOf(theme.mode) + 1) % order.length]);
}

export function setTheme(mode: ThemeMode): void {
	theme.mode = mode;
	applyTheme(mode);
	try {
		if (mode === 'system') localStorage.removeItem(storageKey);
		else localStorage.setItem(storageKey, mode);
	} catch {
		// хранилище недоступно — выбор живёт до перезагрузки
	}
}

export function applyTheme(mode: ThemeMode): void {
	if (mode === 'system') delete document.documentElement.dataset.theme;
	else document.documentElement.dataset.theme = mode;
}
