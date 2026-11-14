/** Одиночные горячие клавиши страницы: `/` — поиск, `n` — добавить. Регистрирует страница, слушает layout. */
type Handler = () => void;

const handlers = new Map<string, Handler[]>();

/** Регистрирует обработчик клавиши на время жизни компонента (вызывать из `$effect`). */
export function hotkey(key: string, handler: Handler): () => void {
	const list = handlers.get(key) ?? [];
	list.push(handler);
	handlers.set(key, list);
	return () => {
		const current = handlers.get(key);
		if (!current) return;
		const index = current.lastIndexOf(handler);
		if (index >= 0) current.splice(index, 1);
	};
}

/** Пользователь печатает в поле — одиночные клавиши не перехватываем. */
export function isTyping(target: EventTarget | null): boolean {
	if (!(target instanceof HTMLElement)) return false;
	return (
		target.isContentEditable ||
		target.closest('input, textarea, select, [contenteditable], .cm-editor') !== null
	);
}

/** Есть открытый модальный диалог — страничные клавиши не работают. */
function modalOpen(): boolean {
	return document.querySelector('dialog[open]') !== null;
}

/** Вызывает последний зарегистрированный обработчик клавиши. true — клавиша обработана. */
export function dispatchHotkey(event: KeyboardEvent): boolean {
	if (event.ctrlKey || event.metaKey || event.altKey) return false;
	if (isTyping(event.target) || modalOpen()) return false;
	const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;
	const alias = key === 'т' ? 'n' : key === '.' && event.code === 'Slash' ? '/' : key;
	const list = handlers.get(alias);
	const handler = list?.[list.length - 1];
	if (!handler) return false;
	event.preventDefault();
	handler();
	return true;
}
