<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import { connectionKinds, interfaceState } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { attempt, attemptOk } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type Connection = components['schemas']['ConnectionDto'];

	const connections = new Resource(() => unwrap(api.GET('/api/connections')), {
		live: ['connections', 'servers']
	});
	const servers = new Resource(() => unwrap(api.GET('/api/servers')), { live: ['servers'] });

	let editorOpen = $state(false);
	let editingId = $state<string>();
	let form = $state({ name: '', kind: 'Socks', serverId: '' });
	let saving = $state(false);

	$effect(() => {
		const timer = setInterval(() => void connections.reload(), 5000);
		return () => clearInterval(timer);
	});

	function openCreate() {
		editingId = undefined;
		form = { name: '', kind: 'Socks', serverId: servers.data?.[0]?.id ?? '' };
		editorOpen = true;
	}

	function openEdit(item: Connection) {
		editingId = item.id;
		form = { name: item.name, kind: item.kind, serverId: item.serverId };
		editorOpen = true;
	}

	async function save() {
		saving = true;
		const id = editingId;
		const name = form.name.trim();
		const saved = await attempt(
			() =>
				id
					? unwrap(
							api.PUT('/api/connections/{id}', {
								params: { path: { id } },
								body: { name, serverId: form.serverId }
							})
						)
					: unwrap(
							api.POST('/api/connections', {
								body: { name, kind: form.kind, serverId: form.serverId }
							})
						),
			id ? 'Соединение изменено' : 'Соединение создано'
		);
		saving = false;
		if (!saved) return;
		editorOpen = false;
		void connections.reload();
	}

	async function remove(item: Connection) {
		if (!confirm(`Удалить соединение «${item.name}» и интерфейс ${item.interfaceName} в Keenetic?`))
			return;
		const done = await attemptOk(
			() => unwrap(api.DELETE('/api/connections/{id}', { params: { path: { id: item.id } } })),
			'Соединение удалено'
		);
		if (done) void connections.reload();
	}
</script>

<div class="toolbar">
	<h1>Соединения</h1>
	<button class="secondary" onclick={() => connections.reload()}>Обновить</button>
	<button onclick={openCreate} disabled={!servers.data?.length}>Создать</button>
</div>

<p class="muted intro">
	Соединение — inbound Xray и свой интерфейс Keenetic (Proxy или WireGuard), на который назначаются
	DNS-маршруты.
</p>

<Loadable resource={connections}>
	{#snippet children(list)}
		{#if list.length === 0}
			<p class="card empty">
				{servers.data?.length === 0 ? 'Сначала добавьте сервер или подписку.' : 'Соединений нет.'}
			</p>
		{:else}
			<div class="table-wrap">
				<table>
					<thead>
						<tr>
							<th>Имя</th>
							<th>Тип</th>
							<th>Сервер</th>
							<th>Интерфейс</th>
							<th>Состояние</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						{#each list as item (item.id)}
							{@const state = interfaceState(item)}
							<tr>
								<td>
									{item.name}
									{#each item.problems as problem (problem)}
										<div class="error small">{problem}</div>
									{/each}
								</td>
								<td>
									{connectionKinds[item.kind] ?? item.kind}
									{#if !item.supportsUdp}
										<Badge title="QUIC и звонки через это соединение не пройдут">только TCP</Badge>
									{/if}
								</td>
								<td>
									{#if item.serverName}{item.serverName}{:else}<Badge tone="danger"
											>сервер удалён</Badge
										>{/if}
								</td>
								<td><code>{item.interfaceName}</code> <span class="muted">:{item.port}</span></td>
								<td>
									<Badge tone={state.tone}>{state.label}</Badge>
									{#if item.kind === 'Wireguard' && item.interface?.lastHandshakeSecondsAgo != null}
										<div class="muted small">
											рукопожатие {item.interface.lastHandshakeSecondsAgo} с назад
										</div>
									{/if}
								</td>
								<td class="actions">
									<button class="secondary small" onclick={() => openEdit(item)}>Изменить</button>
									<button class="danger small" onclick={() => remove(item)}>Удалить</button>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}
	{/snippet}
</Loadable>

<Dialog bind:open={editorOpen} title={editingId ? 'Соединение' : 'Новое соединение'}>
	<div class="form">
		<label>
			Имя
			<input bind:value={form.name} maxlength="64" placeholder="Например, YouTube" />
			<span class="hint">Попадёт в описание интерфейса Keenetic. Без кавычек.</span>
		</label>
		<label>
			Тип
			<select bind:value={form.kind} disabled={!!editingId}>
				<option value="Socks">SOCKS5 — интерфейс Proxy, только TCP</option>
				<option value="Http">HTTP — интерфейс Proxy, только TCP</option>
				<option value="Wireguard">WireGuard — переносит UDP (QUIC, звонки)</option>
			</select>
			{#if editingId}<span class="hint">Тип после создания не меняется.</span>{/if}
		</label>
		<label>
			Сервер
			<select bind:value={form.serverId}>
				{#each servers.data ?? [] as server (server.id)}
					<option value={server.id}>{server.name} — {server.protocol}</option>
				{/each}
			</select>
		</label>
	</div>
	{#snippet actions()}
		<button class="secondary" onclick={() => (editorOpen = false)}>Отмена</button>
		<button disabled={saving || !form.name.trim() || !form.serverId} onclick={save}>
			{editingId ? 'Сохранить' : 'Создать'}
		</button>
	{/snippet}
</Dialog>

<style>
	.intro {
		margin-top: 0;
	}

	.small {
		font-size: 0.85em;
	}
</style>
