import type { components } from '../api/schema';
import { listKey, type Preset, type SetupList } from './presets.js';

export type ServerPreview = components['schemas']['ServerPreviewDto'];
type Server = components['schemas']['ServerDto'];
export type ServerMode = 'subscription' | 'links' | 'existing';
export type WizardStep = 'welcome' | 'server' | 'traffic' | 'run';

/** Сколько соединений можно завести в мастере. */
export const maxConnections = 4;

/** Соединение, которое создаст мастер. */
export interface ConnectionDraft {
	/** Локальный ключ: по нему списки ссылаются на соединение до его создания. */
	uid: string;
	/** Ключ идентичности узла предпросмотра или id имеющегося сервера (режим `existing`). */
	serverRef: string;
	kind: string;
}

/** Выбранный список и соединение, через которое его пустить. */
export interface SelectedList extends SetupList {
	/** Название для показа: сервис плитки или имя списка. */
	title: string;
	connectionUid: string;
}

/** Сервер, который можно выбрать для соединения. */
export interface ServerOption {
	ref: string;
	name: string;
	endpoint: string;
}

/** Ответы пользователя. Живут только в памяти: адрес подписки содержит токен. */
export interface SetupDraft {
	serverMode: ServerMode;
	subscriptionUrl: string;
	linksText: string;
	/** Предпросмотр серверов и ввод, для которого он получен: правка ввода прячет устаревший список. */
	preview?: ServerPreview;
	previewInput: string;
	connections: ConnectionDraft[];
	useUpdateProxy: boolean;
	selected: SelectedList[];
}

export type RunStepId = 'xray' | 'servers' | 'proxy' | 'connection' | 'lists' | 'routes' | 'apply';
export type RunStepStatus = 'pending' | 'running' | 'done' | 'skipped' | 'failed';

export interface RunStep {
	id: RunStepId;
	title: string;
	status: RunStepStatus;
	/** Текущий шаг фоновой задачи или итог шага. */
	detail: string;
	error?: string;
}

/** Ход запуска. Созданное сохраняется, чтобы «Повторить» не создавало дублей. */
export interface RunProgress {
	phase: 'idle' | 'running' | 'failed' | 'done';
	steps: RunStep[];
	/** Созданная подписка: повтор обновляет её, а не создаёт вторую. */
	subscriptionId?: string;
	/** Адрес, с которым создана подписка: другой адрес при повторе — новая подписка. */
	subscriptionUrl?: string;
	/** uid соединения мастера → id сохранённого сервера. */
	serverIds: Record<string, string>;
	/** uid соединения мастера → созданное или подхваченное соединение. */
	connections: Record<string, { id: string; name: string; serverId: string; kind: string }>;
	/** Ключ списка → маршрут (созданный или уже существовавший) и соединение, через которое он идёт. */
	routes: Record<string, { id: string; connectionId: string }>;
	/** Ключи выбранных списков, которых не оказалось в источнике. */
	missingLists?: string[];
	warnings: string[];
}

const dismissKey = 'flowgate-setup-dismissed';

export const runStepTitles: Record<RunStepId, string> = {
	xray: 'Установка Xray',
	servers: 'Серверы',
	proxy: 'Прокси обновлений',
	connection: 'Соединения',
	lists: 'Списки доменов',
	routes: 'DNS-маршруты',
	apply: 'Применение в Keenetic'
};

const kindTitles: Record<string, string> = {
	Wireguard: 'WireGuard',
	Socks: 'SOCKS5',
	Http: 'HTTP'
};

/** Название типа соединения для показа. */
export function kindTitle(kind: string): string {
	return kindTitles[kind] ?? kind;
}

let uidSeq = 0;

/** Новое соединение мастера с WireGuard по умолчанию. */
export function newConnection(serverRef = ''): ConnectionDraft {
	uidSeq++;
	return { uid: `c${uidSeq}`, serverRef, kind: 'Wireguard' };
}

function emptyDraft(): SetupDraft {
	return {
		serverMode: 'subscription',
		subscriptionUrl: '',
		linksText: '',
		preview: undefined,
		previewInput: '',
		connections: [newConnection()],
		useUpdateProxy: true,
		selected: []
	};
}

export function emptyRun(): RunProgress {
	return {
		phase: 'idle',
		steps: (Object.keys(runStepTitles) as RunStepId[]).map((id) => ({
			id,
			title: runStepTitles[id],
			status: 'pending',
			detail: ''
		})),
		serverIds: {},
		connections: {},
		routes: {},
		warnings: []
	};
}

function endpointOf(server: { protocol: string; address: string; port: number | string | null }) {
	return `${server.protocol}, ${server.address}${server.port ? `:${server.port}` : ''}`;
}

/** Состояние мастера быстрой настройки: одно на приложение. */
class SetupWizard {
	open = $state(false);
	step = $state<WizardStep>('server');
	draft = $state<SetupDraft>(emptyDraft());
	run = $state<RunProgress>(emptyRun());
	/** Серверы, которые уже есть в FlowGate (заполняет окно мастера). */
	existingServers = $state<Server[]>([]);

	/** Ввод, к которому относится предпросмотр. */
	input = $derived(
		this.draft.serverMode === 'subscription'
			? this.draft.subscriptionUrl.trim()
			: this.draft.linksText.trim()
	);

	/** Предпросмотр, полученный для того, что сейчас в поле ввода. */
	preview = $derived(
		this.draft.preview && this.draft.previewInput === `${this.draft.serverMode}:${this.input}`
			? this.draft.preview
			: undefined
	);

	/** Серверы, из которых выбираются соединения. */
	serverOptions = $derived<ServerOption[]>(
		this.draft.serverMode === 'existing'
			? this.existingServers.map((server) => ({
					ref: server.id,
					name: server.name,
					endpoint: endpointOf(server)
				}))
			: (this.preview?.servers ?? []).map((server) => ({
					ref: server.importKey,
					name: server.name,
					endpoint: endpointOf(server)
				}))
	);

	/** У каждого соединения выбран сервер из текущего перечня. */
	connectionsReady = $derived(
		this.draft.connections.length > 0 &&
			this.draft.connections.every((connection) =>
				this.serverOptions.some((option) => option.ref === connection.serverRef)
			)
	);

	/** Два соединения с одним сервером и типом дали бы один и тот же интерфейс. */
	duplicateConnections = $derived.by(() => {
		const keys = this.draft.connections.map((item) => `${item.serverRef}|${item.kind}`);
		return keys.some((key, index) => keys.indexOf(key) !== index);
	});

	/** Все выбранные списки. */
	lists = $derived<SelectedList[]>(this.draft.selected);

	/** Выбранные сервисы для показа, без повторов. */
	services = $derived.by<string[]>(() => {
		const titles = this.draft.selected.map((item) => item.title);
		return titles.filter((title, index) => titles.indexOf(title) === index);
	});

	/** Имя сервера соединения. */
	serverName(connection: ConnectionDraft): string {
		return this.serverOptions.find((option) => option.ref === connection.serverRef)?.name ?? '';
	}

	/** Подпись соединения: имя созданного, иначе сервер (и тип, если сервер повторяется). */
	connectionLabel(connection: ConnectionDraft): string {
		const created = this.run.connections[connection.uid];
		if (created) return created.name;
		const name = this.serverName(connection) || 'сервер не выбран';
		const sameServer = this.draft.connections.filter(
			(item) => item.serverRef === connection.serverRef
		).length;
		return sameServer > 1 ? `${name}, ${kindTitle(connection.kind)}` : name;
	}

	isSelected(list: SetupList): boolean {
		const key = listKey(list);
		return this.draft.selected.some((item) => listKey(item) === key);
	}

	/** Добавляет список; без явного соединения — через первое. */
	addList(list: SetupList, title = list.name, connectionUid?: string) {
		if (this.isSelected(list)) return;
		this.draft.selected = [
			...this.draft.selected,
			{ ...list, title, connectionUid: connectionUid ?? this.draft.connections[0]?.uid ?? '' }
		];
	}

	removeList(list: SetupList) {
		const key = listKey(list);
		this.draft.selected = this.draft.selected.filter((item) => listKey(item) !== key);
	}

	presetOn(preset: Preset): boolean {
		return preset.lists.every((list) => this.isSelected(list));
	}

	togglePreset(preset: Preset) {
		if (this.presetOn(preset)) for (const list of preset.lists) this.removeList(list);
		else for (const list of preset.lists) this.addList(list, preset.title);
	}

	/** Новое соединение берёт сервер, который ещё не занят. */
	addConnection() {
		if (this.draft.connections.length >= maxConnections) return;
		const used = this.draft.connections.map((item) => item.serverRef);
		const next =
			this.serverOptions.find((option) => !used.includes(option.ref)) ?? this.serverOptions[0];
		this.draft.connections = [...this.draft.connections, newConnection(next?.ref ?? '')];
	}

	/** Убирает соединение; его списки переходят на первое. */
	removeConnection(uid: string) {
		if (this.draft.connections.length <= 1) return;
		this.draft.connections = this.draft.connections.filter((item) => item.uid !== uid);
		const first = this.draft.connections[0].uid;
		for (const item of this.draft.selected)
			if (item.connectionUid === uid) item.connectionUid = first;
	}

	/** После нового перечня серверов соединения без сервера из него получают свободный сервер. */
	fillServers() {
		const options = this.serverOptions;
		const used: string[] = [];
		for (const connection of this.draft.connections) {
			if (!options.some((option) => option.ref === connection.serverRef))
				connection.serverRef =
					options.find((option) => !used.includes(option.ref))?.ref ?? options[0]?.ref ?? '';
			used.push(connection.serverRef);
		}
	}

	/** Открывает мастер; приветствие — только при автооткрытии. */
	start(welcome = false) {
		if (this.run.phase === 'done') this.reset();
		if (this.run.phase === 'idle') this.step = welcome ? 'welcome' : 'server';
		this.open = true;
	}

	/** Мастер больше не открывается сам. */
	dismiss() {
		try {
			localStorage.setItem(dismissKey, '1');
		} catch {
			// без хранилища мастер снова откроется после перезагрузки
		}
	}

	get dismissed(): boolean {
		try {
			return localStorage.getItem(dismissKey) === '1';
		} catch {
			return false;
		}
	}

	reset() {
		this.draft = emptyDraft();
		this.run = emptyRun();
		this.step = 'server';
	}
}

export const setup = new SetupWizard();
