import { ApiError, errorMessage } from './api/errors.js';

export type ToastTone = 'ok' | 'error' | 'info';

export interface ToastAction {
	label: string;
	run: () => void;
}

export interface Toast {
	id: number;
	tone: ToastTone;
	text: string;
	detail?: string;
	action?: ToastAction;
}

const lifetimeMs = { ok: 4000, info: 6000, error: 12000 } as const;
let nextId = 1;

export const toasts = $state<Toast[]>([]);

export function dismiss(id: number): void {
	const index = toasts.findIndex((toast) => toast.id === id);
	if (index >= 0) toasts.splice(index, 1);
}

export function notify(tone: ToastTone, text: string, detail?: string, action?: ToastAction): void {
	const id = nextId++;
	toasts.push({ id, tone, text, detail, action });
	setTimeout(() => dismiss(id), lifetimeMs[tone]);
}

export function notifyError(error: unknown): void {
	const detail =
		error instanceof ApiError
			? [...error.details, error.traceId ? `traceId ${error.traceId}` : '']
					.filter(Boolean)
					.join('\n')
			: undefined;
	notify('error', errorMessage(error), detail || undefined);
}

/** Копирует текст в буфер обмена и сообщает об этом. */
export async function copyText(text: string, what = 'Скопировано'): Promise<void> {
	try {
		await navigator.clipboard.writeText(text);
		notify('ok', what);
	} catch {
		notify('error', 'Браузер не дал доступ к буферу обмена', 'Скопируйте вручную: ' + text);
	}
}

/** Выполняет действие пользователя: ошибка — в уведомление, успех — опционально сообщением. */
export async function attempt<T>(
	action: () => Promise<T>,
	success?: string
): Promise<T | undefined> {
	try {
		const result = await action();
		if (success) notify('ok', success);
		return result;
	} catch (error) {
		notifyError(error);
		return undefined;
	}
}

/** Как {@link attempt}, но для действий без тела ответа (204): возвращает true при успехе. */
export async function attemptOk(
	action: () => Promise<unknown>,
	success?: string
): Promise<boolean> {
	const result = await attempt(async () => {
		await action();
		return true;
	}, success);
	return result === true;
}
