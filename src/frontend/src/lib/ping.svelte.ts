import { api } from './api/client.js';
import { unwrap } from './api/errors.js';
import { toNumber } from './format.js';
import type { Tone } from './labels.js';

/** Результат пинга сервера в памяти сессии. */
export interface PingResult {
	latencyMs?: number;
	error?: string;
	at: number;
}

/** Сколько серверов уходит в один запрос: бэкенд принимает до 25, ответ приходит по мере пачек. */
const batchSize = 20;

/** Результаты по серверу: живут, пока открыта вкладка. */
export const pings = $state<Record<string, PingResult>>({});

/** Серверы, которые пингуются прямо сейчас. */
export const pinging = $state<Record<string, true>>({});

/**
 * Пингует серверы пачками и заполняет `pings` по мере ответов. Ошибка запроса (например, Xray не установлен)
 * пробрасывается: её показывает `attempt()` у вызывающего.
 */
export async function pingServers(ids: string[]): Promise<void> {
	const queue = ids.filter((id) => !pinging[id]);
	for (const id of queue) pinging[id] = true;
	try {
		for (let i = 0; i < queue.length; i += batchSize) {
			const batch = queue.slice(i, i + batchSize);
			const results = await unwrap(api.POST('/api/servers/ping', { body: { serverIds: batch } }));
			const at = Date.now();
			for (const result of results) {
				pings[result.serverId] = {
					latencyMs: result.latencyMs == null ? undefined : toNumber(result.latencyMs),
					error: result.error ?? undefined,
					at
				};
				delete pinging[result.serverId];
			}
		}
	} finally {
		for (const id of queue) delete pinging[id];
	}
}

/** Подпись и цвет результата пинга. */
export function pingLabel(result: PingResult): { label: string; tone: Tone } {
	if (result.latencyMs == null) return { label: 'нет ответа', tone: 'danger' };
	const tone: Tone = result.latencyMs < 300 ? 'ok' : result.latencyMs < 800 ? 'warn' : 'danger';
	return { label: `${result.latencyMs} мс`, tone };
}
