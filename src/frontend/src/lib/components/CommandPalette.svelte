<script lang="ts">
	import { tick } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { allLinks } from '#lib/nav.js';
	import { cycleTheme } from '#lib/theme.svelte.js';
	import { attempt } from '#lib/toast.svelte.js';
	import { matchesQuery } from '#lib/table.svelte.js';
	import Icon, { type IconName } from './Icon.svelte';

	interface Props {
		open: boolean;
	}

	let { open = $bindable() }: Props = $props();

	interface Command {
		id: string;
		group: string;
		title: string;
		subtitle?: string;
		icon: IconName;
		keywords?: string;
		run: () => void | Promise<unknown>;
	}

	let dialog: HTMLDialogElement;
	let input = $state<HTMLInputElement>();
	let query = $state('');
	let cursor = $state(0);
	let entities = $state<Command[]>([]);

	const actions: Command[] = [
		{
			id: 'a-apply',
			group: 'Действия',
			title: 'Применить все DNS-маршруты',
			subtitle: 'Синхронизация групп и правил с Keenetic',
			icon: 'route',
			keywords: 'применить маршруты keenetic',
			run: () => trackJob(api.POST('/api/dns-routes/apply'))
		},
		{
			id: 'a-restart',
			group: 'Действия',
			title: 'Перезапустить Xray',
			icon: 'refresh',
			keywords: 'restart xray перезапуск',
			run: () => attempt(() => unwrap(api.POST('/api/xray/restart')), 'Xray перезапущен')
		},
		{
			id: 'a-route',
			group: 'Действия',
			title: 'Новый DNS-маршрут',
			icon: 'plus',
			keywords: 'добавить создать маршрут',
			run: () => goto(resolve('/routes?new=1'))
		},
		{
			id: 'a-server',
			group: 'Действия',
			title: 'Добавить сервер',
			icon: 'plus',
			keywords: 'новый сервер outbound',
			run: () => goto(resolve('/servers/[id]', { id: 'new' }))
		},
		{
			id: 'a-import',
			group: 'Действия',
			title: 'Импорт серверов',
			icon: 'upload',
			keywords: 'vless trojan ссылки импорт',
			run: () => goto(resolve('/servers?import=1'))
		},
		{
			id: 'a-subscription',
			group: 'Действия',
			title: 'Добавить подписку',
			icon: 'plus',
			keywords: 'новая подписка провайдер',
			run: () => goto(resolve('/subscriptions?new=1'))
		},
		{
			id: 'a-theme',
			group: 'Действия',
			title: 'Переключить тему',
			subtitle: 'Системная → светлая → тёмная',
			icon: 'sun',
			keywords: 'тема тёмная светлая',
			run: cycleTheme
		}
	];

	const pages: Command[] = allLinks.map((link) => ({
		id: `p-${link.path}`,
		group: 'Разделы',
		title: link.title,
		icon: link.icon,
		keywords: link.keywords,
		run: () => goto(resolve(link.path))
	}));

	const results = $derived.by(() => {
		const all = [...pages, ...actions, ...entities];
		const matched = all.filter((command) =>
			matchesQuery(query, [command.title, command.subtitle, command.keywords, command.group])
		);
		// Без запроса объекты не показываем — только разделы и действия.
		return query.trim()
			? matched.slice(0, 40)
			: matched.filter((item) => !item.id.startsWith('e-'));
	});

	const grouped = $derived.by(() => {
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- временная карта внутри $derived
		const map = new Map<string, { command: Command; index: number }[]>();
		results.forEach((command, index) => {
			const list = map.get(command.group) ?? [];
			list.push({ command, index });
			map.set(command.group, list);
		});
		return [...map.entries()];
	});

	$effect(() => {
		if (open && !dialog.open) {
			dialog.showModal();
			query = '';
			cursor = 0;
			void loadEntities();
			void tick().then(() => input?.focus());
		}
		if (!open && dialog.open) dialog.close();
	});

	$effect(() => {
		void query;
		cursor = 0;
	});

	async function loadEntities() {
		const [servers, routes, sources, subscriptions, connections] = await Promise.all([
			orEmpty(unwrap(api.GET('/api/servers'))),
			orEmpty(unwrap(api.GET('/api/dns-routes'))),
			orEmpty(unwrap(api.GET('/api/domain-sources'))),
			orEmpty(unwrap(api.GET('/api/subscriptions'))),
			orEmpty(unwrap(api.GET('/api/connections')))
		]);
		entities = [
			...servers.map((server) => ({
				id: `e-server-${server.id}`,
				group: 'Серверы',
				title: server.name,
				subtitle: `${server.protocol} · ${server.address}`,
				icon: 'server' as const,
				run: () => goto(resolve('/servers/[id]', { id: server.id }))
			})),
			...routes.map((route) => ({
				id: `e-route-${route.id}`,
				group: 'DNS-маршруты',
				title: route.listName,
				subtitle: `${route.sourceName ?? '—'} → ${route.connectionName ?? '—'}`,
				icon: 'route' as const,
				keywords: route.groupName,
				run: () => goto(resolve(`/routes?q=${encodeURIComponent(route.listName)}`))
			})),
			...sources.map((source) => ({
				id: `e-source-${source.id}`,
				group: 'Источники',
				title: source.name,
				icon: 'layers' as const,
				run: () => goto(resolve('/sources/[id]', { id: source.id }))
			})),
			...subscriptions.map((item) => ({
				id: `e-sub-${item.id}`,
				group: 'Подписки',
				title: item.displayName,
				icon: 'subscription' as const,
				run: () => goto(resolve(`/subscriptions?q=${encodeURIComponent(item.displayName)}`))
			})),
			...connections.map((item) => ({
				id: `e-conn-${item.id}`,
				group: 'Соединения',
				title: item.name,
				subtitle: item.interfaceName,
				icon: 'plug' as const,
				run: () => goto(resolve(`/connections?q=${encodeURIComponent(item.name)}`))
			}))
		];
	}

	/** Недоступный список не ломает палитру: ищем по остальным. */
	async function orEmpty<T>(load: Promise<T[]>): Promise<T[]> {
		try {
			return await load;
		} catch {
			return [];
		}
	}

	function choose(command: Command | undefined) {
		if (!command) return;
		open = false;
		void command.run();
	}

	function onkeydown(event: KeyboardEvent) {
		if (event.key === 'ArrowDown') cursor = (cursor + 1) % Math.max(results.length, 1);
		else if (event.key === 'ArrowUp')
			cursor = (cursor - 1 + results.length) % Math.max(results.length, 1);
		else if (event.key === 'Enter') choose(results[cursor]);
		else return;
		event.preventDefault();
		void tick().then(() =>
			dialog.querySelector('[aria-selected="true"]')?.scrollIntoView({ block: 'nearest' })
		);
	}
</script>

<dialog
	bind:this={dialog}
	class="palette"
	aria-label="Поиск и команды"
	onclose={() => (open = false)}
	onclick={(event) => event.target === dialog && (open = false)}
>
	{#if open}
		<div class="search">
			<Icon name="search" />
			<input
				bind:this={input}
				bind:value={query}
				placeholder="Поиск разделов, объектов и команд"
				role="combobox"
				aria-expanded="true"
				aria-controls="palette-results"
				aria-activedescendant={results[cursor] ? `cmd-${results[cursor].id}` : undefined}
				{onkeydown}
			/>
			<kbd>Esc</kbd>
		</div>
		<div class="results" id="palette-results" role="listbox" aria-label="Результаты">
			{#each grouped as [group, items] (group)}
				<div class="group" role="presentation">{group}</div>
				{#each items as { command, index } (command.id)}
					<button
						id="cmd-{command.id}"
						class="result"
						role="option"
						aria-selected={index === cursor}
						tabindex="-1"
						onpointermove={() => (cursor = index)}
						onclick={() => choose(command)}
					>
						<Icon name={command.icon} size={16} />
						<span class="title">{command.title}</span>
						{#if command.subtitle}<span class="subtitle">{command.subtitle}</span>{/if}
						{#if index === cursor}<Icon name="arrowRight" size={14} class="go" />{/if}
					</button>
				{/each}
			{:else}
				<p class="nothing">Ничего не найдено</p>
			{/each}
		</div>
		<footer>
			<span><kbd>↑</kbd><kbd>↓</kbd> выбрать</span>
			<span><kbd>Enter</kbd> открыть</span>
		</footer>
	{/if}
</dialog>

<style>
	.palette {
		width: min(620px, calc(100vw - 24px));
		max-height: min(560px, calc(100dvh - 120px));
		margin: 12vh auto auto;
		padding: 0;
		border: 1px solid var(--line);
		border-radius: var(--r-lg);
		background: var(--surface);
		color: var(--ink);
		box-shadow: var(--shadow-pop);
		overflow: hidden;
	}

	.palette[open] {
		display: flex;
		flex-direction: column;
		animation: pop 0.14s var(--ease);
	}

	.palette::backdrop {
		background: var(--backdrop);
	}

	.search {
		display: flex;
		align-items: center;
		gap: 10px;
		padding: 0 16px;
		border-bottom: 1px solid var(--line);
		color: var(--faint);
	}

	.search input {
		height: 54px;
		padding: 0;
		border: 0;
		background: none;
		font-size: 16px;
		box-shadow: none !important;
	}

	.results {
		flex: 1;
		overflow-y: auto;
		padding: 6px;
	}

	.group {
		padding: 10px 10px 4px;
		color: var(--faint);
		font-size: 12px;
		font-weight: 500;
	}

	.result {
		display: flex;
		align-items: center;
		gap: 10px;
		width: 100%;
		min-height: 38px;
		padding: 0 10px;
		border-radius: var(--r-sm);
		text-align: left;
	}

	.result :global(.icon) {
		color: var(--faint);
	}

	.result[aria-selected='true'] {
		background: var(--accent-soft);
	}

	.result[aria-selected='true'] :global(.icon) {
		color: var(--accent-text);
	}

	.title {
		white-space: nowrap;
	}

	.subtitle {
		flex: 1;
		min-width: 0;
		overflow: hidden;
		color: var(--muted);
		font-size: 12.5px;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.result :global(.go) {
		margin-left: auto;
	}

	.nothing {
		padding: 28px 16px;
		color: var(--muted);
		text-align: center;
	}

	footer {
		display: flex;
		gap: 16px;
		padding: 8px 16px;
		border-top: 1px solid var(--line);
		background: var(--surface-2);
		color: var(--faint);
		font-size: 12px;
	}

	footer kbd {
		margin-right: 3px;
	}

	@keyframes pop {
		from {
			opacity: 0;
			transform: scale(0.98);
		}
	}
</style>
