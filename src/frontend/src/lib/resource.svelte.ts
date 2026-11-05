/** Загружаемые данные страницы: состояние загрузки, ошибка и повторная загрузка без мигания старых данных. */
export class Resource<T> {
	data = $state<T | undefined>(undefined);
	error = $state<unknown>(undefined);
	loading = $state(true);

	#load: () => Promise<T>;

	constructor(load: () => Promise<T>) {
		this.#load = load;
		void this.reload();
	}

	async reload(): Promise<void> {
		this.loading = true;
		try {
			this.data = await this.#load();
			this.error = undefined;
		} catch (error) {
			this.error = error;
		} finally {
			this.loading = false;
		}
	}
}
