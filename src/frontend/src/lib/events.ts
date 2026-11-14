/** Темы изменений бэкенда: имя JSON-файла данных без расширения или служебная тема. */
export type ChangeTopic =
	| 'connections'
	| 'dns-routes'
	| 'domain-sources'
	| 'local-lists'
	| 'servers'
	| 'settings'
	| 'subscriptions'
	| 'jobs'
	| 'xray';

type Handler = () => void;

const handlers = new Map<ChangeTopic, Set<Handler>>();
let source: EventSource | undefined;
let lostConnection = false;

/**
 * Подписывает обработчик на изменения тем. После переподключения потока обработчик вызывается один раз,
 * потому что изменения за время обрыва потеряны. Возвращает функцию отписки.
 */
export function onChange(topics: readonly ChangeTopic[], handler: Handler): () => void {
	for (const topic of topics) {
		let set = handlers.get(topic);
		if (!set) handlers.set(topic, (set = new Set()));
		set.add(handler);
	}
	connect();

	return () => {
		for (const topic of topics) handlers.get(topic)?.delete(handler);
		if ([...handlers.values()].every((set) => set.size === 0)) disconnect();
	};
}

function connect(): void {
	if (source || typeof EventSource === 'undefined') return;
	source = new EventSource('/api/events');
	source.addEventListener('open', () => {
		if (lostConnection) {
			lostConnection = false;
			notifyAll();
		}
	});
	source.addEventListener('error', () => {
		lostConnection = true;
	});
	source.addEventListener('change', (event) => {
		const set = handlers.get((event as MessageEvent<string>).data as ChangeTopic);
		for (const handler of set ?? []) handler();
	});
}

function disconnect(): void {
	source?.close();
	source = undefined;
	lostConnection = false;
}

function notifyAll(): void {
	const unique = new Set<Handler>();
	for (const set of handlers.values()) for (const handler of set) unique.add(handler);
	for (const handler of unique) handler();
}
