<script lang="ts">
	import { onMount, tick } from 'svelte';
	import { resolve } from '$app/paths';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { runSetup } from '#lib/setup/run.js';
	import { kindTitle, setup, type RunStepId, type WizardStep } from '#lib/setup/setup.svelte.js';
	import Dialog from '../Dialog.svelte';
	import Icon from '../Icon.svelte';
	import PathRail, { type PathNode, type PathTone } from './PathRail.svelte';
	import RunStep from './RunStep.svelte';
	import ServerStep from './ServerStep.svelte';
	import TrafficStep from './TrafficStep.svelte';

	const servers = new Resource(() => unwrap(api.GET('/api/servers')), { live: ['servers'] });
	const xray = new Resource(() => unwrap(api.GET('/api/xray')), { live: ['xray'] });
	const keenetic = new Resource(() => unwrap(api.GET('/api/keenetic/system')));

	const steps: { id: WizardStep; title: string; text: string }[] = [
		{
			id: 'server',
			title: 'Серверы и соединения',
			text: 'Откуда взять VPN-серверы и сколько соединений создать. Подойдёт подписка провайдера или ссылки на серверы.'
		},
		{
			id: 'traffic',
			title: 'Что открывать через VPN',
			text: 'Выберите сервисы и соединение для каждого. Остальные сайты продолжат открываться напрямую.'
		},
		{
			id: 'run',
			title: 'Запуск',
			text: 'Проверьте выбор. До нажатия «Настроить» на роутере ничего не меняется.'
		}
	];

	let heading = $state<HTMLHeadingElement>();

	const draft = $derived(setup.draft);
	const run = $derived(setup.run);
	const lists = $derived(setup.lists);
	const running = $derived(run.phase === 'running');
	const stepIndex = $derived(steps.findIndex((step) => step.id === setup.step));
	const current = $derived(steps[stepIndex]);

	/** Имена серверов соединений без повторов. */
	const serverNames = $derived.by(() => {
		const names = draft.connections
			.map((connection) => setup.serverName(connection))
			.filter(Boolean);
		return names.filter((name, index) => names.indexOf(name) === index);
	});
	const connectionsValue = $derived.by(() => {
		const created = draft.connections
			.map((connection) => run.connections[connection.uid]?.name)
			.filter(Boolean);
		if (created.length === draft.connections.length) return created.join(', ');
		const count = draft.connections.length;
		return count === 1 ? kindTitle(draft.connections[0].kind) : `${count} соединения`;
	});

	// Перечень имеющихся серверов нужен шагу «Серверы» и подписям соединений.
	$effect(() => {
		setup.existingServers = servers.data ?? [];
	});

	/** Подзаголовок шага; на шаге запуска он следует за ходом. */
	const lead = $derived(
		setup.step !== 'run' || run.phase === 'idle'
			? (current?.text ?? '')
			: run.phase === 'running'
				? 'FlowGate настраивает роутер. Окно можно будет закрыть, когда настройка закончится.'
				: run.phase === 'failed'
					? 'Настройка остановилась на шаге с ошибкой. Устраните причину и нажмите «Повторить»: готовые шаги не выполняются заново.'
					: 'Всё на месте. Изменить настройки можно в разделах слева.'
	);
	const unsupported = $derived(keenetic.data && !keenetic.data.isSupported);

	const canNext = $derived(
		setup.step === 'server'
			? setup.connectionsReady && !setup.duplicateConnections
			: setup.step === 'traffic'
				? lists.length > 0
				: true
	);

	function tone(ids: RunStepId[], filled: boolean): PathTone {
		if (run.phase === 'idle') return filled ? 'filled' : 'empty';
		const statuses = run.steps.filter((step) => ids.includes(step.id)).map((step) => step.status);
		if (statuses.includes('failed')) return 'failed';
		if (statuses.includes('running')) return 'running';
		if (statuses.every((status) => status === 'done' || status === 'skipped')) return 'ok';
		return filled ? 'filled' : 'empty';
	}

	const nodes = $derived<PathNode[]>([
		{
			key: 'lists',
			icon: 'list',
			title: 'Сервисы',
			value:
				setup.services.length === 0
					? 'не выбраны'
					: setup.services.length === 1
						? setup.services[0]
						: `${setup.services[0]} и ещё ${setup.services.length - 1}`,
			tone: tone(['lists'], lists.length > 0)
		},
		{
			key: 'routes',
			icon: 'route',
			title: 'DNS-маршруты',
			value: lists.length > 0 ? `${lists.length} в Keenetic` : '—',
			tone: tone(['routes', 'apply'], lists.length > 0)
		},
		{
			key: 'connection',
			icon: 'plug',
			title: draft.connections.length > 1 ? 'Соединения' : 'Соединение',
			value: connectionsValue,
			tone: tone(['connection'], true)
		},
		{
			key: 'xray',
			icon: 'core',
			title: 'Xray',
			value: xray.data?.installed ? (xray.data.version ?? 'установлен') : 'будет установлен',
			tone: tone(['xray'], true)
		},
		{
			key: 'server',
			icon: 'server',
			title: serverNames.length > 1 ? 'Серверы' : 'Сервер',
			value:
				serverNames.length === 0
					? 'не выбран'
					: serverNames.length === 1
						? serverNames[0]
						: `${serverNames[0]} и ещё ${serverNames.length - 1}`,
			tone: tone(['servers', 'proxy'], setup.connectionsReady)
		}
	]);

	onMount(() => {
		void autoOpen();
	});

	// Новый шаг — фокус на его заголовок, чтобы клавиатура и экранный диктор начинали сверху.
	$effect(() => {
		void setup.step;
		if (setup.open) void tick().then(() => heading?.focus());
	});

	/** Первый вход: ничего не настроено и мастер не откладывали. */
	async function autoOpen() {
		if (setup.dismissed) return;
		try {
			const [serverList, connections, routes] = await Promise.all([
				unwrap(api.GET('/api/servers')),
				unwrap(api.GET('/api/connections')),
				unwrap(api.GET('/api/dns-routes'))
			]);
			if (serverList.length === 0 && connections.length === 0 && routes.length === 0)
				setup.start(true);
		} catch {
			// нет ответа — мастер можно открыть с Обзора
		}
	}

	function go(step: WizardStep) {
		setup.step = step;
	}

	function next() {
		if (setup.step === 'welcome') go('server');
		else if (setup.step === 'server' && canNext) go('traffic');
		else if (setup.step === 'traffic' && canNext) go('run');
		else if (setup.step === 'run' && run.phase !== 'running' && run.phase !== 'done') void start();
	}

	function back() {
		if (running) return;
		// После сбоя можно поправить ответы: созданное запомнено, шаги пройдут заново и подхватят его.
		if (run.phase === 'failed') {
			for (const step of run.steps) step.status = 'pending';
			run.phase = 'idle';
		}
		if (setup.step === 'run') go('traffic');
		else if (setup.step === 'traffic') go('server');
	}

	async function start() {
		if (run.subscriptionUrl && run.subscriptionUrl !== draft.subscriptionUrl.trim())
			run.subscriptionId = undefined;
		if (draft.serverMode === 'subscription') run.subscriptionUrl = draft.subscriptionUrl.trim();
		run.warnings = [];
		const ok = await runSetup(draft, lists, run);
		if (ok) {
			setup.dismiss();
			void xray.reload();
		}
	}

	function later() {
		setup.dismiss();
		setup.open = false;
	}

	// Закрытое окно больше не открывается само.
	let wasOpen = false;
	$effect(() => {
		if (setup.open) wasOpen = true;
		else if (wasOpen) setup.dismiss();
	});
</script>

<Dialog
	bind:open={setup.open}
	title="Быстрая настройка"
	size="xl"
	flush
	dismissible={!running}
	onsubmit={next}
>
	<div class="wizard">
		<aside class="rail">
			<PathRail {nodes} />
			<p class="rail-caption muted small">Путь трафика: от списка доменов до сервера.</p>
		</aside>

		<div class="content">
			{#if setup.step === 'welcome'}
				<h3 bind:this={heading} tabindex="-1">Настроим FlowGate за три шага</h3>
				<p class="lead muted">
					Вы выберете сервер и сервисы, которые открывать через VPN, а мастер сам установит Xray,
					создаст соединение и DNS-маршруты в Keenetic. До последнего шага на роутере ничего не
					меняется.
				</p>
				<dl class="props checks">
					<dt>Роутер</dt>
					<dd>
						{#if keenetic.data}
							{keenetic.data.model}, KeeneticOS {keenetic.data.release}
						{:else if keenetic.error}
							<span class="error-text">нет ответа</span>
						{:else}
							<span class="muted">проверка…</span>
						{/if}
					</dd>
					<dt>Xray</dt>
					<dd>
						{#if xray.data?.installed}
							установлен, {xray.data.version}
						{:else if xray.data}
							будет установлен с GitHub
						{:else}
							<span class="muted">проверка…</span>
						{/if}
					</dd>
				</dl>
				{#if unsupported}
					<p class="blocker" role="alert">
						<Icon name="alert" size={16} />
						<span>
							Эта версия KeeneticOS не поддерживается: FlowGate нужна KeeneticOS 5.
							{#each keenetic.data?.problems ?? [] as problem (problem)}<br />{problem}{/each}
						</span>
					</p>
				{/if}
			{:else if current}
				<h3 bind:this={heading} tabindex="-1">{current.title}</h3>
				<p class="lead muted">{lead}</p>
				{#if setup.step === 'server'}
					<ServerStep />
				{:else if setup.step === 'traffic'}
					<TrafficStep />
				{:else}
					<RunStep />
				{/if}
			{/if}
		</div>
	</div>

	{#snippet actions()}
		{#if current}
			<span class="counter muted small">Шаг {stepIndex + 1} из {steps.length}</span>
		{/if}
		{#if setup.step === 'welcome'}
			<button class="btn ghost" onclick={later}>Позже</button>
			<button class="btn primary" disabled={!!unsupported} onclick={next}>Начать</button>
		{:else if run.phase === 'done'}
			<a class="btn" href={resolve('/routes')} onclick={() => (setup.open = false)}>К маршрутам</a>
			<button class="btn primary" onclick={() => (setup.open = false)}>Готово</button>
		{:else}
			{#if setup.step !== 'server'}
				<button class="btn ghost" disabled={running} onclick={back}>
					<Icon name="arrowLeft" size={16} />Назад
				</button>
			{/if}
			{#if setup.step === 'run'}
				<button class="btn primary" aria-busy={running} disabled={running} onclick={next}>
					{run.phase === 'failed' ? 'Повторить' : 'Настроить'}
				</button>
			{:else}
				<button class="btn primary" disabled={!canNext} onclick={next}>
					Далее<Icon name="arrowRight" size={16} />
				</button>
			{/if}
		{/if}
	{/snippet}
</Dialog>

<style>
	.wizard {
		display: grid;
		grid-template-columns: 248px minmax(0, 1fr);
		flex: 1;
		min-height: 0;
	}

	.rail {
		display: grid;
		align-content: start;
		gap: 20px;
		padding: 28px 24px;
		border-right: 1px solid var(--line);
		background: var(--surface-2);
	}

	.content {
		min-width: 0;
		overflow: auto;
		padding: 28px 32px 32px;
	}

	.content h3 {
		font-size: 19px;
		font-weight: 650;
		letter-spacing: -0.01em;
		outline: none;
	}

	.lead {
		max-width: 60ch;
		margin: 4px 0 24px;
	}

	.checks {
		grid-template-columns: max-content minmax(0, 1fr);
		gap: 10px 24px;
	}

	.blocker {
		display: flex;
		gap: 10px;
		margin-top: 20px;
		padding: 12px 14px;
		border-radius: var(--r-sm);
		background: var(--danger-soft);
		color: var(--danger);
	}

	.blocker :global(.icon) {
		flex: none;
		margin-top: 3px;
	}

	.counter {
		margin-right: auto;
		align-self: center;
	}

	@media (max-width: 720px) {
		.wizard {
			grid-template-columns: minmax(0, 1fr);
			grid-template-rows: auto minmax(0, 1fr);
		}

		.rail {
			padding: 14px 16px;
			border-right: 0;
			border-bottom: 1px solid var(--line);
		}

		.rail-caption {
			display: none;
		}

		.content {
			padding: 20px 16px 24px;
		}
	}
</style>
