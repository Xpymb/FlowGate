export interface ConfirmOptions {
	title: string;
	message?: string;
	/** Текст кнопки подтверждения: глагол действия, например «Удалить». */
	action: string;
	danger?: boolean;
}

interface ConfirmState extends ConfirmOptions {
	open: boolean;
	resolve?: (ok: boolean) => void;
}

/** Состояние единственного диалога подтверждения; его рисует `ConfirmHost` в layout. */
export const confirmState = $state<ConfirmState>({ open: false, title: '', action: '' });

/** Спрашивает подтверждение у пользователя. true — подтвердил. */
export function ask(options: ConfirmOptions): Promise<boolean> {
	confirmState.resolve?.(false);
	return new Promise((resolve) => {
		Object.assign(confirmState, { message: undefined, danger: false }, options, {
			open: true,
			resolve
		});
	});
}

export function settle(ok: boolean): void {
	const resolve = confirmState.resolve;
	confirmState.open = false;
	confirmState.resolve = undefined;
	resolve?.(ok);
}
