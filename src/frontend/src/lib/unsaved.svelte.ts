import { beforeNavigate } from '$app/navigation';

/**
 * Предупреждает об уходе со страницы с несохранёнными правками: переход внутри приложения спрашивает подтверждение,
 * закрытие вкладки — стандартный вопрос браузера. Вызывать при инициализации компонента.
 */
export function guardUnsaved(isDirty: () => boolean): void {
	beforeNavigate((navigation) => {
		if (!isDirty()) return;
		if (navigation.type === 'leave') {
			navigation.cancel();
			return;
		}
		if (!window.confirm('Несохранённые изменения будут потеряны. Покинуть страницу?'))
			navigation.cancel();
	});
}
