import createClient, { type Middleware } from 'openapi-fetch';

import { session } from '#lib/session.svelte.js';
import type { paths } from './schema';

/** Отмечает, что сессии нет: nginx отвечает 401 на любой /api без входа. */
const unauthorized: Middleware = {
	onResponse({ response }) {
		if (response.status === 401) {
			session.unauthorized = true;
		}
		return undefined;
	}
};

/** Клиент API FlowGate. Пути относительные: в проде их проксирует nginx, в разработке — Vite. */
export const api = createClient<paths>({ baseUrl: '/', credentials: 'same-origin' });

api.use(unauthorized);
