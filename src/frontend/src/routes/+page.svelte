<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { formatDateTime, formatDuration, formatKilobytes, toNumber } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { interfaceState, label, xrayStates, type Tone } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { notify } from '#lib/toast.svelte.js';
	import Badge from '#lib/components/Badge.svelte';
	import Icon, { type IconName } from '#lib/components/Icon.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';

	const system = new Resource(() => unwrap(api.GET('/api/system')), { live: ['xray'] });
	const xray = new Resource(() => unwrap(api.GET('/api/xray')), { live: ['xray'] });
	const keenetic = new Resource(() => unwrap(api.GET('/api/keenetic/system')));
	const servers = new Resource(() => unwrap(api.GET('/api/servers')), { live: ['servers'] });
	const connections = new Resource(() => unwrap(api.GET('/api/connections')), {
		live: ['connections', 'servers']
	});
	const routes = new Resource(() => unwrap(api.GET('/api/dns-routes')), {
		live: ['dns-routes', 'connections', 'domain-sources']
	});
	const subscriptions = new Resource(() => unwrap(api.GET('/api/subscriptions')), {
		live: ['subscriptions']
	});
	const sources = new Resource(() => unwrap(api.GET('/api/domain-sources')), {
		live: ['domain-sources']
	});

	let upgrading = $state(false);

	// Интерфейсы Keenetic в события не попадают — опрашиваем, как на «Соединениях».
	$effect(() => {
		const timer = setInterval(() => void connections.reload(), 10000);
		return () => clearInterval(timer);
	});

	interface FlowNode {
		key: string;
		icon: IconName;
		title: string;
		value: string;
		detail: string;
		tone: Tone;
		href: string;
	}

	interface Issue {
		tone: 'danger' | 'warn';
		text: string;
		href: string;
		action: string;
	}

	const xrayState = $derived(xray.data ? label(xrayStates, xray.data.state) : undefined);
	const xrayRunning = $derived(xray.data?.state === 'Running');

	const routeStats = $derived.by(() => {
		const list = routes.data ?? [];
		const enabled = list.filter((route) => route.state === 'Enabled').length;
		const failing = list.filter(
			(route) => route.lastApply && route.lastApply.status !== 'Ok'
		).length;
		return { total: list.length, enabled, failing };
	});

	const connectionStats = $derived.by(() => {
		const list = connections.data ?? [];
		const online = list.filter((item) => interfaceState(item).tone === 'ok').length;
		return { total: list.length, online };
	});

	const nodes = $derived<FlowNode[]>([
		{
			key: 'routes',
			icon: 'route',
			title: 'DNS-маршруты',
			value: routes.data ? `${routeStats.enabled} из ${routeStats.total}` : '…',
			detail: routeStats.total === 0 ? 'ещё не созданы' : 'правил включено',
			tone:
				routeStats.total === 0
					? 'muted'
					: routeStats.failing > 0
						? 'warn'
						: routeStats.enabled > 0
							? 'ok'
							: 'muted',
			href: resolve('/routes')
		},
		{
			key: 'connections',
			icon: 'plug',
			title: 'Соединения',
			value: connections.data ? `${connectionStats.online} из ${connectionStats.total}` : '…',
			detail: connectionStats.total === 0 ? 'ещё не созданы' : 'на связи',
			tone:
				connectionStats.total === 0
					? 'muted'
					: connectionStats.online === connectionStats.total
						? 'ok'
						: connectionStats.online === 0
							? 'danger'
							: 'warn',
			href: resolve('/connections')
		},
		{
			key: 'xray',
			icon: 'core',
			title: 'Xray',
			value: xrayState?.label ?? '…',
			detail: xray.data?.version ?? (xray.data?.installed === false ? 'не установлен' : ''),
			tone: xrayState?.tone ?? 'muted',
			href: resolve('/xray')
		},
		{
			key: 'servers',
			icon: 'server',
			title: 'Серверы',
			value: servers.data ? String(servers.data.length) : '…',
			detail: `подписок: ${subscriptions.data?.length ?? '…'}`,
			tone: servers.data?.length ? 'ok' : 'muted',
			href: resolve('/servers')
		}
	]);

	const issues = $derived.by<Issue[]>(() => {
		const list: Issue[] = [];
		if (xray.data && !xray.data.installed)
			list.push({
				tone: 'danger',
				text: 'Xray не установлен — трафик не идёт',
				href: resolve('/xray'),
				action: 'Установить'
			});
		else if (xray.data && xray.data.state === 'Failed')
			list.push({
				tone: 'danger',
				text: `Xray упал: ${xray.data.lastError ?? 'без подробностей'}`,
				href: resolve('/xray'),
				action: 'Журнал'
			});
		else if (xray.data && xray.data.state === 'Stopped')
			list.push({
				tone: 'warn',
				text: 'Xray остановлен',
				href: resolve('/xray'),
				action: 'Запустить'
			});

		for (const problem of keenetic.data?.problems ?? [])
			list.push({ tone: 'danger', text: `Роутер: ${problem}`, href: resolve('/'), action: '' });

		const down = (connections.data ?? []).filter((item) => interfaceState(item).tone !== 'ok');
		if (down.length > 0)
			list.push({
				tone: 'warn',
				text:
					down.length === 1
						? `Соединение «${down[0].name}»: ${interfaceState(down[0]).label}`
						: `Соединений не на связи: ${down.length}`,
				href: resolve('/connections'),
				action: 'Проверить'
			});

		const brokenRoutes = (routes.data ?? []).filter(
			(route) =>
				!route.connectionName ||
				!route.sourceName ||
				(route.lastApply && route.lastApply.status === 'Failed')
		);
		if (brokenRoutes.length > 0)
			list.push({
				tone: 'danger',
				text: `Маршруты с ошибкой применения или без соединения: ${brokenRoutes.length}`,
				href: resolve('/routes?state=issues'),
				action: 'Открыть'
			});

		for (const item of subscriptions.data ?? []) {
			if (item.lastRefresh?.status === 'Failed')
				list.push({
					tone: 'warn',
					text: `Подписка «${item.displayName}» не обновилась`,
					href: resolve(`/subscriptions?q=${encodeURIComponent(item.displayName)}`),
					action: 'Открыть'
				});
			const expire = item.provider?.expireAt ? new Date(item.provider.expireAt).getTime() : NaN;
			const days = (expire - Date.now()) / 86_400_000;
			if (days <= 7)
				list.push({
					tone: days <= 0 ? 'danger' : 'warn',
					text:
						days <= 0
							? `Подписка «${item.displayName}» истекла`
							: `Подписка «${item.displayName}» истекает через ${Math.ceil(days)} дн.`,
					href: resolve(`/subscriptions?q=${encodeURIComponent(item.displayName)}`),
					action: 'Открыть'
				});
		}

		const failedSources = (sources.data ?? []).filter(
			(source) => source.lastRefresh?.status === 'Failed'
		);
		if (failedSources.length > 0)
			list.push({
				tone: 'warn',
				text: `Источники не обновились: ${failedSources.map((s) => s.name).join(', ')}`,
				href: resolve('/sources'),
				action: 'Открыть'
			});

		return list;
	});

	const loaded = $derived(
		!!(xray.data && connections.data && routes.data && subscriptions.data && sources.data)
	);

	const updates = $derived.by(() => {
		const list: { title: string; version: string; href?: string }[] = [];
		if (xray.data?.updateAvailable && xray.data.latestVersion)
			list.push({ title: 'Xray', version: xray.data.latestVersion, href: resolve('/xray') });
		return list;
	});

	async function upgrade(version: string | null) {
		const ok = await ask({
			title: `Обновить FlowGate до ${version ?? 'новой версии'}?`,
			message:
				'Служба перезапустится, интерфейс будет недоступен около минуты. Трафик через Xray не прервётся.',
			action: 'Обновить'
		});
		if (!ok) return;
		upgrading = true;
		const done = await trackJob(api.POST('/api/system/update'));
		if (done) notify('info', 'Обновление идёт через opkg. Обновите страницу через минуту.');
		else upgrading = false;
	}
</script>

<PageHeader title="Обзор" description="Как сейчас идёт трафик и что требует вашего внимания." />

<section class="flow" aria-label="Путь трафика">
	{#each nodes as node, index (node.key)}
		{#if index > 0}
			<div
				class="link {nodes[index - 1].tone} "
				class:moving={xrayRunning && nodes[index - 1].tone === 'ok' && node.tone === 'ok'}
				aria-hidden="true"
			>
				<span class="line"></span>
				<Icon name="chevronRight" size={16} />
			</div>
		{/if}
		<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- адрес уже собран через resolve() -->
		<a class="node {node.tone}" href={node.href}>
			<span class="node-head">
				<span class="node-icon"><Icon name={node.icon} size={18} /></span>
				<span class="node-title">{node.title}</span>
				<span class="led" aria-hidden="true"></span>
			</span>
			<span class="node-value num">{node.value}</span>
			<span class="node-detail">{node.detail || ' '}</span>
		</a>
	{/each}
</section>
<p class="flow-caption muted small">
	Сайты из списков → правило DNS-маршрута в Keenetic → интерфейс соединения → Xray → сервер.
</p>

<div class="columns">
	<section class="panel attention">
		<header class="panel-head">
			<h2>Требует внимания</h2>
			{#if loaded && issues.length > 0}<Badge tone="warn">{issues.length}</Badge>{/if}
		</header>
		{#if !loaded}
			<p class="panel-body muted">Собираем состояние…</p>
		{:else if issues.length === 0}
			<div class="all-good">
				<span class="ok-mark"><Icon name="check" size={20} /></span>
				<div>
					<h3>Всё работает</h3>
					<p class="muted">Xray запущен, соединения на связи, маршруты применены без ошибок.</p>
				</div>
			</div>
		{:else}
			<ul class="issues">
				{#each issues as issue (issue.text)}
					<li class={issue.tone}>
						<Icon name="alert" size={16} />
						<span>{issue.text}</span>
						{#if issue.action}
							<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- адрес уже собран через resolve() -->
							<a class="btn sm" href={issue.href}>{issue.action}</a>
						{/if}
					</li>
				{/each}
			</ul>
		{/if}
	</section>

	<div class="side stack">
		<section class="panel panel-body">
			<h2 class="mini-title">FlowGate</h2>
			{#if system.data}
				{@const status = system.data}
				<dl class="props">
					<dt>Версия</dt>
					<dd>
						<span class="mono">{status.version}</span>
						{#if status.updateAvailable}<Badge tone="accent">есть {status.latestVersion}</Badge
							>{/if}
					</dd>
					<dt>Работает</dt>
					<dd>{formatDuration(status.uptimeSeconds)}</dd>
					<dt>Память</dt>
					<dd>
						{formatKilobytes(status.memoryRssKilobytes)}
						<span class="muted small">+ Xray {formatKilobytes(status.xrayMemoryRssKilobytes)}</span>
					</dd>
					<dt>Проверено</dt>
					<dd>{formatDateTime(status.updateCheckedAt)}</dd>
					{#if status.lastUpgradeResult}
						<dt>Обновление</dt>
						<dd class="small">{status.lastUpgradeResult}</dd>
					{/if}
				</dl>
				{#if status.updateAvailable && status.packageInstalled}
					<button
						class="btn primary full"
						aria-busy={upgrading}
						onclick={() => upgrade(status.latestVersion)}
					>
						<Icon name="download" size={16} />Обновить до {status.latestVersion}
					</button>
				{/if}
				{#each updates as update (update.title)}
					<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- адрес уже собран через resolve() -->
					<a class="update" href={update.href}>
						<Icon name="download" size={15} />Доступен {update.title}
						{update.version}
					</a>
				{/each}
			{:else if system.error}
				<p class="error-text">FlowGate не ответил. Обновите страницу.</p>
			{:else}
				<p class="muted">Загрузка…</p>
			{/if}
		</section>

		<section class="panel panel-body">
			<h2 class="mini-title">Роутер</h2>
			{#if keenetic.data}
				{@const info = keenetic.data}
				<dl class="props">
					<dt>Модель</dt>
					<dd>{info.model}</dd>
					<dt>KeeneticOS</dt>
					<dd class="mono">{info.release}</dd>
					<dt>Совместимость</dt>
					<dd>
						{#if info.isSupported}<Badge tone="ok" dot>поддерживается</Badge>
						{:else}<Badge tone="danger" dot>не поддерживается</Badge>{/if}
					</dd>
				</dl>
			{:else if keenetic.error}
				<p class="error-text">Роутер не ответил на запрос FlowGate.</p>
			{:else}
				<p class="muted">Загрузка…</p>
			{/if}
		</section>

		{#if sources.data}
			<section class="panel panel-body">
				<h2 class="mini-title">Списки</h2>
				<dl class="props">
					<dt>Источников</dt>
					<dd class="num">{sources.data.length}</dd>
					<dt>Списков</dt>
					<dd class="num">
						{sources.data.reduce((sum, source) => sum + (toNumber(source.listCount) ?? 0), 0)}
					</dd>
				</dl>
			</section>
		{/if}
	</div>
</div>

<style>
	/* Схема потока — единственный выразительный элемент интерфейса. */
	.flow {
		display: grid;
		grid-template-columns: 1fr auto 1fr auto 1fr auto 1fr;
		align-items: stretch;
	}

	.node {
		display: grid;
		gap: 4px;
		padding: 18px 18px 16px;
		border: 1px solid var(--line);
		border-radius: var(--r-lg);
		background: var(--surface);
		color: var(--ink);
		transition: border-color 0.15s;
	}

	.node:hover {
		border-color: var(--line-strong);
		text-decoration: none;
	}

	.node-head {
		display: flex;
		align-items: center;
		gap: 10px;
		margin-bottom: 10px;
	}

	.node-icon {
		display: grid;
		place-items: center;
		width: 32px;
		height: 32px;
		border-radius: 9px;
		background: var(--hover);
		color: var(--muted);
	}

	.node.ok .node-icon {
		background: var(--accent-soft);
		color: var(--accent-text);
	}

	.node.warn .node-icon,
	.node.accent .node-icon {
		background: var(--warn-soft);
		color: var(--warn);
	}

	.node.danger .node-icon {
		background: var(--danger-soft);
		color: var(--danger);
	}

	.node-title {
		flex: 1;
		color: var(--muted);
		font-weight: 500;
	}

	.led {
		width: 8px;
		height: 8px;
		border-radius: 999px;
		background: var(--faint);
	}

	.node.ok .led {
		background: var(--ok);
		box-shadow: 0 0 0 3px var(--ok-soft);
	}

	.node.warn .led,
	.node.accent .led {
		background: var(--warn);
		box-shadow: 0 0 0 3px var(--warn-soft);
	}

	.node.danger .led {
		background: var(--danger);
		box-shadow: 0 0 0 3px var(--danger-soft);
	}

	.node-value {
		font-size: 26px;
		font-weight: 650;
		line-height: 1.1;
		letter-spacing: -0.015em;
	}

	.node-detail {
		color: var(--muted);
		font-size: 13px;
	}

	.link {
		position: relative;
		display: flex;
		align-items: center;
		width: 36px;
		color: var(--line-strong);
	}

	.link .line {
		position: absolute;
		left: 0;
		right: 6px;
		top: 50%;
		height: 2px;
		background: var(--line-strong);
	}

	.link :global(.icon) {
		position: absolute;
		right: -4px;
	}

	.link.ok {
		color: var(--accent);
	}

	.link.ok .line {
		background: var(--accent);
		opacity: 0.45;
	}

	/* Трафик идёт: по связи бегут штрихи. */
	.link.moving .line {
		opacity: 1;
		background: repeating-linear-gradient(90deg, var(--accent) 0 6px, transparent 6px 11px);
		background-size: 22px 2px;
		animation: flow 0.9s linear infinite;
	}

	@keyframes flow {
		to {
			background-position: 22px 0;
		}
	}

	.flow-caption {
		margin: 12px 2px 32px;
	}

	.columns {
		display: grid;
		grid-template-columns: minmax(0, 1fr) 340px;
		gap: 20px;
		align-items: start;
	}

	.all-good {
		display: flex;
		align-items: center;
		gap: 16px;
		padding: 28px 20px;
	}

	.ok-mark {
		display: grid;
		place-items: center;
		width: 44px;
		height: 44px;
		border-radius: 12px;
		background: var(--ok-soft);
		color: var(--ok);
	}

	.issues {
		margin: 0;
		padding: 0;
		list-style: none;
	}

	.issues li {
		display: flex;
		align-items: center;
		gap: 12px;
		min-height: 56px;
		padding: 10px 14px 10px 20px;
		border-top: 1px solid var(--line);
	}

	.issues li:first-child {
		border-top: 0;
	}

	.issues li span {
		flex: 1;
	}

	.issues .danger > :global(.icon) {
		color: var(--danger);
	}

	.issues .warn > :global(.icon) {
		color: var(--warn);
	}

	.side {
		gap: 16px;
	}

	.side .panel-body {
		display: grid;
		gap: 14px;
	}

	.mini-title {
		font-size: 14px;
	}

	.side dl.props {
		grid-template-columns: max-content 1fr;
		gap: 8px 16px;
		font-size: 13.5px;
	}

	.side dd {
		display: flex;
		align-items: center;
		gap: 6px;
		flex-wrap: wrap;
	}

	.full {
		width: 100%;
	}

	.update {
		display: flex;
		align-items: center;
		gap: 8px;
		font-size: 13px;
	}

	@media (max-width: 1100px) {
		.columns {
			grid-template-columns: 1fr;
		}
	}

	@media (max-width: 900px) {
		.flow {
			grid-template-columns: 1fr;
		}

		.link {
			justify-content: center;
			width: auto;
			height: 24px;
		}

		.link .line {
			left: 50%;
			right: auto;
			top: 0;
			bottom: 4px;
			width: 2px;
			height: auto;
		}

		.link.moving .line {
			background: repeating-linear-gradient(180deg, var(--accent) 0 6px, transparent 6px 11px);
			background-size: 2px 22px;
			animation-name: flow-down;
		}

		.link :global(.icon) {
			right: auto;
			bottom: -6px;
			transform: rotate(90deg);
		}
	}

	@keyframes flow-down {
		to {
			background-position: 0 22px;
		}
	}
</style>
