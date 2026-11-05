<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import { Resource } from '#lib/resource.svelte.js';
	import { attempt, notify } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type ImportResult = components['schemas']['ServerImportResultDto'];

	const servers = new Resource(() => unwrap(api.GET('/api/servers')));
	const subscriptions = new Resource(() => unwrap(api.GET('/api/subscriptions')));

	let filter = $state('');
	let importOpen = $state(false);
	let importText = $state('');
	let importing = $state(false);
	let importResult = $state<ImportResult>();

	const subscriptionNames = $derived(
		new Map((subscriptions.data ?? []).map((item) => [item.id, item.displayName]))
	);

	function matches(server: components['schemas']['ServerDto']): boolean {
		const query = filter.trim().toLowerCase();
		if (!query) return true;
		return [server.name, server.address, server.protocol, server.network]
			.join(' ')
			.toLowerCase()
			.includes(query);
	}

	async function runImport() {
		importing = true;
		const result = await attempt(() =>
			unwrap(api.POST('/api/servers/import', { body: { text: importText } }))
		);
		importing = false;
		if (!result) return;
		importResult = result;
		notify('ok', `Добавлено серверов: ${result.added.length}`);
		void servers.reload();
	}

	function openImport() {
		importText = '';
		importResult = undefined;
		importOpen = true;
	}

	async function remove(id: string, name: string) {
		if (!confirm(`Удалить сервер «${name}»?`)) return;
		const done = await attempt(
			() => unwrap(api.DELETE('/api/servers/{id}', { params: { path: { id } } })),
			'Сервер удалён'
		);
		if (done !== undefined) void servers.reload();
	}
</script>

<div class="toolbar">
	<h1>Серверы</h1>
	<input class="filter" placeholder="Поиск" bind:value={filter} />
	<button class="secondary" onclick={openImport}>Импорт ссылок</button>
	<a class="button" href={resolve('/servers/[id]', { id: 'new' })}>Добавить</a>
</div>

<Loadable resource={servers}>
	{#snippet children(list)}
		{@const visible = list.filter(matches)}
		{#if list.length === 0}
			<p class="card empty">
				Серверов нет. Импортируйте ссылки или добавьте подписку на странице «Подписки».
			</p>
		{:else}
			<div class="table-wrap">
				<table>
					<thead>
						<tr>
							<th>Имя</th>
							<th>Протокол</th>
							<th>Адрес</th>
							<th>Транспорт</th>
							<th>Источник</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						{#each visible as server (server.id)}
							<tr>
								<td>
									<a href={resolve('/servers/[id]', { id: server.id })}>{server.name}</a>
								</td>
								<td>{server.protocol}</td>
								<td class="address">{server.address}{server.port ? `:${server.port}` : ''}</td>
								<td>{server.network} / {server.security}</td>
								<td>
									{#if server.source === 'Subscription'}
										<Badge tone="accent">
											{subscriptionNames.get(server.subscriptionId ?? '') ?? 'подписка'}
										</Badge>
									{:else}
										<Badge>вручную</Badge>
									{/if}
								</td>
								<td>
									{#if server.source !== 'Subscription'}
										<button class="danger small" onclick={() => remove(server.id, server.name)}
											>Удалить</button
										>
									{/if}
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
			{#if visible.length === 0}<p class="empty">Ничего не найдено</p>{/if}
		{/if}
	{/snippet}
</Loadable>

<Dialog bind:open={importOpen} title="Импорт серверов" wide>
	<div class="form">
		<label>
			Ссылки по одной на строку, base64 от них или Xray-JSON
			<textarea bind:value={importText} placeholder="vless://…&#10;trojan://…" rows="8"></textarea>
		</label>
		{#if importResult}
			<div class="result">
				<p>
					Добавлено: {importResult.added.length}, уже были: {importResult.skipped}, отклонено: {importResult
						.rejected.length}
				</p>
				{#if importResult.rejected.length > 0}
					<ul class="plain error">
						{#each importResult.rejected as item (item.index)}
							<li>Узел {item.index}: {item.reason}</li>
						{/each}
					</ul>
				{/if}
				{#if importResult.warnings.length > 0}
					<ul class="plain muted">
						{#each importResult.warnings as warning (warning)}<li>{warning}</li>{/each}
					</ul>
				{/if}
			</div>
		{/if}
	</div>
	{#snippet actions()}
		<button class="secondary" onclick={() => (importOpen = false)}>Закрыть</button>
		<button disabled={importing || !importText.trim()} onclick={runImport}>Импортировать</button>
	{/snippet}
</Dialog>

<style>
	.filter {
		width: 220px;
	}

	.address {
		font-family: var(--mono);
		font-size: 0.9em;
	}

	.result {
		display: grid;
		gap: 6px;
	}

	.result p {
		margin: 0;
	}
</style>
