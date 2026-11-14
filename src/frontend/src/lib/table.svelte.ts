import { goto } from '$app/navigation';
import { page } from '$app/state';
import { SvelteSet } from 'svelte/reactivity';

/**
 * Параметр строки адреса (`?q=…`), связанный с полем страницы: значение переживает обновление и делится ссылкой.
 * Создавать при инициализации компонента.
 */
export class UrlParam {
	#name: string;
	#timer: ReturnType<typeof setTimeout> | undefined;
	value = $state('');

	constructor(name: string) {
		this.#name = name;
		this.value = page.url.searchParams.get(name) ?? '';

		// Адрес сменили снаружи (палитра команд, «назад»): поле догоняет его, если пользователь не печатает.
		$effect(() => {
			const fromUrl = page.url.searchParams.get(this.#name) ?? '';
			if (this.#timer === undefined) this.value = fromUrl;
		});
	}

	set(value: string): void {
		this.value = value;
		clearTimeout(this.#timer);
		this.#timer = setTimeout(() => {
			this.#timer = undefined;
			// eslint-disable-next-line svelte/prefer-svelte-reactivity -- одноразовая сборка адреса
			const url = new URL(page.url.href);
			if (value) url.searchParams.set(this.#name, value);
			else url.searchParams.delete(this.#name);
			if (url.search !== page.url.search) {
				// eslint-disable-next-line svelte/no-navigation-without-resolve -- тот же маршрут, меняется только query
				void goto(url, { replace: true, reset: false });
			}
		}, 250);
	}
}

/** Выбор строк таблицы для массовых действий. */
export class Selection {
	ids = new SvelteSet<string>();

	get size(): number {
		return this.ids.size;
	}

	has(id: string): boolean {
		return this.ids.has(id);
	}

	toggle(id: string): void {
		if (this.ids.has(id)) this.ids.delete(id);
		else this.ids.add(id);
	}

	/** Состояние чекбокса «все» для видимых строк. */
	allState(visible: readonly string[]): 'all' | 'some' | 'none' {
		const count = visible.filter((id) => this.ids.has(id)).length;
		if (count === 0) return 'none';
		return count === visible.length ? 'all' : 'some';
	}

	toggleAll(visible: readonly string[]): void {
		if (this.allState(visible) === 'all') for (const id of visible) this.ids.delete(id);
		else for (const id of visible) this.ids.add(id);
	}

	/** Убирает из выбора строки, которых больше нет. */
	retain(existing: readonly string[]): void {
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- временный набор для сравнения
		const keep = new Set(existing);
		for (const id of [...this.ids]) if (!keep.has(id)) this.ids.delete(id);
	}

	clear(): void {
		this.ids.clear();
	}
}

/** Сортировка таблицы по колонке. */
export class Sorter<K extends string> {
	key = $state<K>() as K;
	desc = $state(false);

	constructor(key: K) {
		this.key = key;
	}

	by(key: K): void {
		if (this.key === key) this.desc = !this.desc;
		else {
			this.key = key;
			this.desc = false;
		}
	}

	aria(key: K): 'ascending' | 'descending' | 'none' {
		if (this.key !== key) return 'none';
		return this.desc ? 'descending' : 'ascending';
	}

	sort<T>(list: readonly T[], value: (item: T, key: K) => string | number): T[] {
		const sign = this.desc ? -1 : 1;
		return [...list].sort((a, b) => {
			const left = value(a, this.key);
			const right = value(b, this.key);
			if (typeof left === 'number' && typeof right === 'number') return (left - right) * sign;
			return String(left).localeCompare(String(right), 'ru', { numeric: true }) * sign;
		});
	}
}

/** Строка подходит под поисковый запрос: все слова запроса встречаются в полях. */
export function matchesQuery(query: string, fields: (string | null | undefined)[]): boolean {
	const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
	if (words.length === 0) return true;
	const haystack = fields.filter(Boolean).join(' ').toLowerCase();
	return words.every((word) => haystack.includes(word));
}
