import type { components } from './schema';

export type ErrorResponse = components['schemas']['ErrorResponse'];

/** Ошибка API в формате бэкенда `{errorCode, message, traceId}`. */
export class ApiError extends Error {
	readonly status: number;
	readonly code: string;
	readonly traceId: string | undefined;
	readonly details: string[];

	constructor(status: number, body: unknown) {
		const parsed = isErrorResponse(body) ? body : undefined;
		super(parsed?.message ?? describeStatus(status, body));
		this.name = 'ApiError';
		this.status = status;
		this.code = parsed?.errorCode ?? (status === 0 ? 'NETWORK_ERROR' : `HTTP_${status}`);
		this.traceId = parsed?.traceId;
		this.details = parsed?.details ?? [];
	}
}

/** Результат вызова openapi-fetch. */
type FetchResult<T> = { data?: T; error?: unknown; response: Response };

/** Возвращает данные ответа или бросает {@link ApiError}. */
export async function unwrap<T>(call: Promise<FetchResult<T>>): Promise<T> {
	const { data, error, response } = await call;
	if (!response.ok) {
		throw new ApiError(response.status, error);
	}
	return data as T;
}

/** Текст ошибки для показа пользователю. */
export function errorMessage(error: unknown): string {
	if (error instanceof ApiError) return error.message;
	if (error instanceof Error) return error.message;
	return String(error);
}

function isErrorResponse(body: unknown): body is ErrorResponse {
	return (
		typeof body === 'object' &&
		body !== null &&
		'errorCode' in body &&
		'message' in body &&
		typeof (body as ErrorResponse).message === 'string'
	);
}

function describeStatus(status: number, body: unknown): string {
	if (status === 401) return 'Нужно войти';
	if (status === 502 || status === 504) return 'FlowGate не отвечает';
	if (typeof body === 'string' && body.length > 0 && body.length < 200) return body;
	return `Ошибка HTTP ${status}`;
}
