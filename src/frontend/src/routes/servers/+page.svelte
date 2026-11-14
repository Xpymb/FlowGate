<script lang="ts">
	import { untrack } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { hotkey } from '#lib/hotkeys.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { matchesQuery, Selection, Sorter, UrlParam } from '#lib/table.svelte.js';
	import { attempt, copyText, notify, notifyError } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import BulkBar from '#lib/components/BulkBar.svelte';
	import Checkbox from '#lib/components/Checkbox.svelte';
	import Copyable from '#lib/components/Copyable.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Field from '#lib/components/Field.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import Menu from '#lib/components/Menu.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SearchInput from '#lib/components/SearchInput.svelte';

	type Server = components['schemas']['ServerDto'];
	type ImportResult = components['schemas']['ServerImportResultDto'];
	type SortKey = 'name' | 'protocol' | 'address' | 'source';

	const servers = new Resource(() => unwrap(api.GET('/api/servers')), { live: ['servers'] });
	const subscriptions = new Resource(() => unwrap(api.GET('/api/subscriptions')), {
		live: ['subscriptions']
	});

	const query = new UrlParam('q');
	const origin = new UrlParam('source');
	const selection = new Selection();
	const sorter = new Sorter<SortKey>('name');

	let importOpen = $state(false);
	let importText = $state('');
	let importing = $state(false);
	let importResult = $state<ImportResult>();
	let deleting = $state(false);

	const subscriptionNames = $derived(
		new Map((subscriptions.data ?? []).map((item) => [item.id, item.displayName]))
	);
	const all = $derived(servers.data ?? []);
	const visible = $derived(
		sorter.sort(
			all.filter(
				(server) =>
					(origin.value === '' ||
						(origin.value === 'manual') === (server.source !== 'Subscription')) &&
					matchesQuery(query.value, [
						server.name,
						server.address,
						server.protocol,
						server.network,
						server.security,
						sourceName(server)
					])
			),
			(server, key) =>
				key === 'address' ? server.address : key === 'source' ? sourceName(server) : server[key]
		)
	);
	const visibleIds = $derived(visible.map((server) => server.id));
	const manualCount = $derived(all.filter((server) => server.source !== 'Subscription').length);
	const selectedManual = $derived(
		all.filter((server) => selection.has(server.id) && server.source !== 'Subscription')
	);

	$effect(() => {
		selection.retain(all.map((server) => server.id));
	});

	$effect(() => hotkey('n', () => void goto(resolve('/servers/[id]', { id: 'new' }))));

	// Палитра команд открывает импорт через ?import=1.
	$effect(() => {
		if (page.url.searchParams.get('import') !== '1') return;
		untrack(() => {
			openImport();
			void goto(resolve('/servers'), { replace: true, reset: false });
		});
	});

	function sourceName(server: Server): string {
		return server.source === 'Subscription'
			? (subscriptionNames.get(server.subscriptionId ?? '') ?? 'подписка')
			: 'вручную';
	}

	function endpoint(server: Server): string {
		return `${server.address}${server.port ? `:${server.port}` : ''}`;
	}

	function openImport() {
		importText = '';
		importResult = undefined;
		importOpen = true;
	}

	async function runImport() {
		if (!importText.trim()) return;
		importing = true;
		const result = await attempt(() =>
			unwrap(api.POST('/api/servers/import', { body: { text: importText } }))
		);
		importing = false;
		if (!result) return;
		importResult = result;
		if (result.added.length > 0) notify('ok', `Добавлено серверов: ${result.added.length}`);
		void servers.reload();
	}

	async function removeOne(server: Server) {
		const ok = await ask({
			title: `Удалить сервер «${server.name}»?`,
			message: 'Соединения, использующие этот сервер, перестанут работать.',
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		await removeMany([server]);
	}

	async function removeSelected() {
		const list = selectedManual;
		const skipped = selection.size - list.length;
		const ok = await ask({
			title: `Удалить серверы: ${list.length}?`,
			message:
				skipped > 0
					? `Серверы из подписок (${skipped}) будут пропущены: ими управляет подписка.`
					: 'Соединения, использующие эти серверы, перестанут работать.',
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		await removeMany(list);
	}

	async function removeMany(list: Server[]) {
		deleting = true;
		let done = 0;
		for (const server of list) {
			try {
				await unwrap(api.DELETE('/api/servers/{id}', { params: { path: { id: server.id } } }));
				selection.ids.delete(server.id);
				done++;
			} catch (error) {
				notifyError(error);
			}
		}
		deleting = false;
		if (done > 0) notify('ok', done === 1 ? 'Сервер удалён' : `Удалено серверов: ${done}`);
		void servers.reload();
	}
</script>

{#snippet sortHead(key: SortKey, title: string)}
	<th aria-sort={sorter.aria(key)}>
		<button class="sort" onclick={() => sorter.by(key)}>
			{title}
			{#if sorter.key === key}<Icon name={sorter.desc ? 'arrowDown' : 'arrowUp'} size={13} />{/if}
		</button>
	</th>
{/snippet}

<PageHeader
	title="Серверы"
	description="Серверы Xray для исходящего трафика соединений. Добавляются вручную, импортом ссылок или из подписок."
>
	{#snippet actions()}
		<button class="btn" onclick={openImport}><Icon name="upload" size={16} />Импорт ссылок</button>
		<a class="btn primary" href={resolve('/servers/[id]', { id: 'new' })} title="Клавиша N">
			<Icon name="plus" size={16} />Добавить сервер
		</a>
	{/snippet}
</PageHeader>

<Loadable resource={servers}>
	{#snippet children(list)}
		{#if list.length === 0}
			<EmptyState
				icon="server"
				title="Нет серверов"
				text="Импортируйте ссылки vless://, trojan:// и других протоколов или добавьте подписку провайдера."
			>
				<button class="btn primary" onclick={openImport}>Импортировать ссылки</button>
				<a class="btn" href={resolve('/subscriptions?new=1')}>Добавить подписку</a>
			</EmptyState>
		{:else}
			<div class="toolbar">
				<SearchInput
					value={query.value}
					placeholder="Имя, адрес, протокол или подписка"
					oninput={(value) => query.set(value)}
				/>
				<div class="segmented" role="group" aria-label="Откуда сервер">
					<button aria-pressed={origin.value === ''} onclick={() => origin.set('')}>Все</button>
					<button aria-pressed={origin.value === 'manual'} onclick={() => origin.set('manual')}>
						Вручную <span class="faint num">{manualCount}</span>
					</button>
					<button
						aria-pressed={origin.value === 'subscription'}
						onclick={() => origin.set('subscription')}
					>
						Из подписок <span class="faint num">{list.length - manualCount}</span>
					</button>
				</div>
				<span class="count muted small num">
					{visible.length === list.length
						? `Серверов: ${list.length}`
						: `Показано ${visible.length} из ${list.length}`}
				</span>
			</div>

			<div class="panel">
				<div class="table-scroll">
					<table class="table cards">
						<thead>
							<tr>
								<th class="check-cell">
									<Checkbox
										label="Выбрать все"
										checked={selection.allState(visibleIds) === 'all'}
										indeterminate={selection.allState(visibleIds) === 'some'}
										onchange={() => selection.toggleAll(visibleIds)}
									/>
								</th>
								{@render sortHead('name', 'Имя')}
								{@render sortHead('protocol', 'Протокол')}
								{@render sortHead('address', 'Адрес')}
								<th>Транспорт</th>
								{@render sortHead('source', 'Источник')}
								<th class="actions-cell"><span class="visually-hidden">Действия</span></th>
							</tr>
						</thead>
						<tbody>
							{#each visible as server (server.id)}
								{@const manual = server.source !== 'Subscription'}
								<tr class:selected={selection.has(server.id)}>
									<td class="check-cell">
										<Checkbox
											label="Выбрать {server.name}"
											checked={selection.has(server.id)}
											onchange={() => selection.toggle(server.id)}
										/>
									</td>
									<td class="primary name">
										<a href={resolve('/servers/[id]', { id: server.id })}>{server.name}</a>
									</td>
									<td data-label="Протокол"><span class="proto">{server.protocol}</span></td>
									<td data-label="Адрес"><Copyable value={endpoint(server)} /></td>
									<td data-label="Транспорт" class="muted">{server.network} · {server.security}</td>
									<td data-label="Источник">
										{#if manual}<Badge>вручную</Badge>
										{:else}<Badge tone="accent">{sourceName(server)}</Badge>{/if}
									</td>
									<td class="actions-cell">
										<Menu
											ariaLabel="Действия с {server.name}"
											items={[
												{
													label: manual ? 'Изменить' : 'Открыть',
													icon: manual ? 'edit' : 'eye',
													href: resolve('/servers/[id]', { id: server.id })
												},
												{
													label: 'Скопировать адрес',
													icon: 'copy',
													onclick: () => copyText(endpoint(server), 'Адрес скопирован')
												},
												'separator',
												{
													label: manual ? 'Удалить' : 'Управляется подпиской',
													icon: 'trash',
													danger: true,
													disabled: !manual,
													onclick: () => removeOne(server)
												}
											]}
										/>
									</td>
								</tr>
							{:else}
								<tr>
									<td colspan="7">
										<EmptyState
											bare
											icon="search"
											title="Ничего не найдено"
											text="Нет серверов, соответствующих условиям."
										>
											<button
												class="btn"
												onclick={() => {
													query.set('');
													origin.set('');
												}}>Сбросить фильтры</button
											>
										</EmptyState>
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			</div>
		{/if}
	{/snippet}
</Loadable>

<BulkBar count={selection.size} onclear={() => selection.clear()}>
	<button
		class="btn sm danger"
		disabled={selectedManual.length === 0}
		aria-busy={deleting}
		onclick={removeSelected}
		title={selectedManual.length === 0
			? 'Серверы из подписок удаляются вместе с подпиской'
			: undefined}
	>
		<Icon name="trash" size={14} />Удалить{selectedManual.length
			? ` (${selectedManual.length})`
			: ''}
	</button>
</BulkBar>

<Dialog
	bind:open={importOpen}
	title="Импорт серверов"
	description="Ссылки по одной на строку, base64 или Xray-JSON с outbounds."
	size="lg"
	onsubmit={runImport}
>
	<div class="form">
		<Field label="Ссылки" hint="Дубликаты пропускаются. Строки с ошибками отображаются ниже.">
			<textarea
				bind:value={importText}
				placeholder="vless://…&#10;trojan://…&#10;ss://…"
				rows="9"
				spellcheck="false"></textarea>
		</Field>
		{#if importResult}
			<div class="result" role="status">
				<div class="stats">
					<span><b class="num">{importResult.added.length}</b> добавлено</span>
					<span><b class="num">{importResult.skipped}</b> дубликатов</span>
					<span class:bad={importResult.rejected.length > 0}>
						<b class="num">{importResult.rejected.length}</b> отклонено
					</span>
				</div>
				{#if importResult.rejected.length > 0}
					<ul class="issues error-text">
						{#each importResult.rejected as item (item.index)}
							<li>Строка {item.index}: {item.reason}</li>
						{/each}
					</ul>
				{/if}
				{#if importResult.warnings.length > 0}
					<ul class="issues muted">
						{#each importResult.warnings as warning (warning)}<li>{warning}</li>{/each}
					</ul>
				{/if}
			</div>
		{/if}
	</div>
	{#snippet actions()}
		<span class="muted small hint-keys"><kbd>Ctrl</kbd> <kbd>Enter</kbd></span>
		<button class="btn" onclick={() => (importOpen = false)}>
			{importResult ? 'Готово' : 'Отмена'}
		</button>
		<button
			class="btn primary"
			disabled={!importText.trim()}
			aria-busy={importing}
			onclick={runImport}>Импортировать</button
		>
	{/snippet}
</Dialog>

<style>
	.toolbar {
		display: flex;
		align-items: center;
		gap: 12px;
		flex-wrap: wrap;
		margin-bottom: 16px;
	}

	.count {
		margin-left: auto;
	}

	.name a {
		color: var(--ink);
		font-weight: 500;
	}

	.proto {
		font-family: var(--mono);
		font-size: 12.5px;
	}

	.result {
		display: grid;
		gap: 10px;
		padding: 14px 16px;
		border-radius: var(--r-md);
		background: var(--surface-2);
		border: 1px solid var(--line);
	}

	.stats {
		display: flex;
		gap: 20px;
		flex-wrap: wrap;
	}

	.stats .bad {
		color: var(--danger);
	}

	.issues {
		display: grid;
		gap: 4px;
		margin: 0;
		padding-left: 18px;
		font-size: 13px;
	}

	.hint-keys {
		margin-right: auto;
		align-self: center;
	}
</style>
