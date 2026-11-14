import { onChange, type ChangeTopic } from './events.js';

/** Через сколько после события изменения перечитывать данные: несколько записей подряд дают одну загрузку. */
const liveDelayMs = 150;

export interface ResourceOptions {
	/**
	 * Темы `/api/events`, при изменении которых данные перечитываются. Подписка живёт, пока жив компонент,
	 * поэтому такой ресурс создаётся только при инициализации компонента.
	 */
	live?: readonly ChangeTopic[];
}

/** Загружаемые данные страницы: состояние загрузки, ошибка и повторная загрузка без мигания старых данных. */
export class Resource<T> {
	data = $state<T | undefined>(undefined);
	error = $state<unknown>(undefined);
	loading = $state(true);

	#load: () => Promise<T>;
	#seq = 0;

	constructor(load: () => Promise<T>, options: ResourceOptions = {}) {
		this.#load = load;
		void this.reload();

		const topics = options.live;
		if (topics?.length) {
			$effect(() => {
				let timer: ReturnType<typeof setTimeout> | undefined;
				const unsubscribe = onChange(topics, () => {
					clearTimeout(timer);
					timer = setTimeout(() => void this.reload(), liveDelayMs);
				});
				return () => {
					clearTimeout(timer);
					unsubscribe();
				};
			});
		}
	}

	/** Загружает данные заново; ответ устаревшего запроса отбрасывается. */
	async reload(): Promise<void> {
		const seq = ++this.#seq;
		this.loading = true;
		try {
			const data = await this.#load();
			if (seq !== this.#seq) return;
			this.data = data;
			this.error = undefined;
		} catch (error) {
			if (seq !== this.#seq) return;
			this.error = error;
		} finally {
			if (seq === this.#seq) this.loading = false;
		}
	}
}
