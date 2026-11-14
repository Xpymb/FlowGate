<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { hotkey } from '#lib/hotkeys.js';
	import { connectionKinds, interfaceState } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { matchesQuery, UrlParam } from '#lib/table.svelte.js';
	import { attempt, attemptOk, copyText } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import Copyable from '#lib/components/Copyable.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Field from '#lib/components/Field.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import Menu from '#lib/components/Menu.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SearchInput from '#lib/components/SearchInput.svelte';

	type Connection = components['schemas']['ConnectionDto'];

	const connections = new Resource(() => unwrap(api.GET('/api/connections')), {
		live: ['connections', 'servers']
	});
	const servers = new Resource(() => unwrap(api.GET('/api/servers')), { live: ['servers'] });
	const query = new UrlParam('q');

	const kinds = [
		{
			value: 'Wireguard',
			title: 'WireGuard',
			text: 'Поддержка TCP и UDP (QUIC, VoIP). Рекомендуется.'
		},
		{ value: 'Socks', title: 'SOCKS5', text: 'Интерфейс Proxy, только TCP.' },
		{ value: 'Http', title: 'HTTP', text: 'Интерфейс Proxy, только TCP.' }
	];

	let editorOpen = $state(false);
	let editingId = $state<string>();
	let form = $state({ name: '', kind: 'Wireguard', serverId: '' });
	let saving = $state(false);
	/** Имя подставлено автоматически и ещё не правилось: при смене типа оно следует за типом. */
	let autoName = $state(false);

	const kindSuffixes: Record<string, string> = { Wireguard: 'wg', Socks: 'socks', Http: 'http' };

	const noServers = $derived(servers.data?.length === 0);
	const visible = $derived(
		(connections.data ?? []).filter((item) =>
			matchesQuery(query.value, [item.name, item.serverName, item.interfaceName, item.kind])
		)
	);

	// Состояние интерфейсов Keenetic в события не попадает — опрашиваем.
	$effect(() => {
		const timer = setInterval(() => void connections.reload(), 5000);
		return () => clearInterval(timer);
	});

	$effect(() => hotkey('n', () => !noServers && openCreate()));

	/** Свободное имя вида xray-wg, xray-wg-2, … по типу соединения. */
	function suggestName(kind: string): string {
		const base = `xray-${kindSuffixes[kind] ?? kind.toLowerCase()}`;
		const taken = new Set((connections.data ?? []).map((item) => item.name.toLowerCase()));
		if (!taken.has(base)) return base;
		let index = 2;
		while (taken.has(`${base}-${index}`)) index++;
		return `${base}-${index}`;
	}

	function openCreate() {
		editingId = undefined;
		form = {
			name: suggestName('Wireguard'),
			kind: 'Wireguard',
			serverId: servers.data?.[0]?.id ?? ''
		};
		autoName = true;
		editorOpen = true;
	}

	function changeKind(kind: string) {
		form.kind = kind;
		if (autoName) form.name = suggestName(kind);
	}

	function openEdit(item: Connection) {
		editingId = item.id;
		form = { name: item.name, kind: item.kind, serverId: item.serverId };
		autoName = false;
		editorOpen = true;
	}

	async function save() {
		const name = form.name.trim();
		if (!name || !form.serverId) return;
		saving = true;
		const id = editingId;
		const result = await attempt(
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
		if (!result) return;
		editorOpen = false;
		void connections.reload();
	}

	async function remove(item: Connection) {
		const ok = await ask({
			title: `Удалить соединение «${item.name}»?`,
			message: `Интерфейс ${item.interfaceName} будет удалён из Keenetic. Маршруты через это соединение перестанут работать.`,
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		const done = await attemptOk(
			() => unwrap(api.DELETE('/api/connections/{id}', { params: { path: { id: item.id } } })),
			'Соединение удалено'
		);
		if (done) void connections.reload();
	}
</script>

<PageHeader
	title="Соединения"
	description="Соединение связывает входящий порт Xray с интерфейсом Keenetic. DNS-маршруты направляют трафик на эти интерфейсы."
>
	{#snippet actions()}
		<button class="btn primary" disabled={noServers} onclick={openCreate} title="Клавиша N">
			<Icon name="plus" size={16} />Создать соединение
		</button>
	{/snippet}
</PageHeader>

<Loadable resource={connections}>
	{#snippet children(list)}
		{#if list.length === 0}
			{#if noServers}
				<EmptyState
					icon="server"
					title="Нет серверов"
					text="Для создания соединения требуется сервер Xray. Добавьте сервер или подписку."
				>
					<a class="btn primary" href={resolve('/servers')}>К серверам</a>
					<a class="btn" href={resolve('/subscriptions?new=1')}>Добавить подписку</a>
				</EmptyState>
			{:else}
				<EmptyState
					icon="plug"
					title="Нет соединений"
					text="Для каждого соединения в Keenetic создаётся интерфейс, на который ссылаются DNS-маршруты."
				>
					<button class="btn primary" onclick={openCreate}>Создать соединение</button>
				</EmptyState>
			{/if}
		{:else}
			<div class="toolbar">
				<SearchInput
					value={query.value}
					placeholder="Имя, сервер или интерфейс"
					oninput={(value) => query.set(value)}
				/>
				<span class="live muted small"
					><span class="pulse"></span>Состояние обновляется каждые 5 с</span
				>
			</div>
			<div class="panel">
				<div class="table-scroll">
					<table class="table cards">
						<thead>
							<tr>
								<th>Соединение</th>
								<th>Сервер</th>
								<th>Интерфейс Keenetic</th>
								<th>Состояние</th>
								<th class="actions-cell"><span class="visually-hidden">Действия</span></th>
							</tr>
						</thead>
						<tbody>
							{#each visible as item (item.id)}
								{@const state = interfaceState(item)}
								<tr>
									<td class="primary">
										<button class="name" onclick={() => openEdit(item)}>{item.name}</button>
										<div class="sub">
											{connectionKinds[item.kind] ?? item.kind}
											{#if !item.supportsUdp}
												· <span title="QUIC и VoIP через это соединение не поддерживаются"
													>только TCP</span
												>
											{/if}
										</div>
										{#each item.problems as problem (problem)}
											<div class="problem small"><Icon name="alert" size={13} />{problem}</div>
										{/each}
									</td>
									<td data-label="Сервер">
										{#if item.serverName}
											<a href={resolve('/servers/[id]', { id: item.serverId })}>{item.serverName}</a
											>
										{:else}<Badge tone="danger">сервер удалён</Badge>{/if}
									</td>
									<td data-label="Интерфейс">
										<Copyable value={item.interfaceName} />
										<div class="sub mono">порт {item.port}</div>
									</td>
									<td data-label="Состояние">
										<Badge tone={state.tone} dot>{state.label}</Badge>
										{#if item.kind === 'Wireguard' && item.interface?.lastHandshakeSecondsAgo != null}
											<div class="sub">
												последнее рукопожатие: {item.interface.lastHandshakeSecondsAgo} с назад
											</div>
										{/if}
									</td>
									<td class="actions-cell">
										<Menu
											ariaLabel="Действия с {item.name}"
											items={[
												{ label: 'Изменить', icon: 'edit', onclick: () => openEdit(item) },
												{
													label: 'Связанные маршруты',
													icon: 'route',
													href: resolve(`/routes?q=${encodeURIComponent(item.name)}`)
												},
												{
													label: 'Скопировать имя интерфейса',
													icon: 'copy',
													onclick: () => copyText(item.interfaceName, 'Имя интерфейса скопировано')
												},
												'separator',
												{
													label: 'Удалить',
													icon: 'trash',
													danger: true,
													onclick: () => remove(item)
												}
											]}
										/>
									</td>
								</tr>
							{:else}
								<tr>
									<td colspan="5">
										<EmptyState bare icon="search" title="Ничего не найдено">
											<button class="btn" onclick={() => query.set('')}>Сбросить поиск</button>
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

<Dialog
	bind:open={editorOpen}
	variant="side"
	title={editingId ? 'Соединение' : 'Новое соединение'}
	description={editingId ? undefined : 'Будут созданы интерфейс Keenetic и входящий порт Xray.'}
	onsubmit={save}
>
	<div class="form">
		<Field
			label="Имя"
			hint="Подставляется по типу соединения, можно изменить. Используется в описании интерфейса Keenetic, кавычки не допускаются."
		>
			<input
				bind:value={form.name}
				maxlength="64"
				placeholder="xray-wg"
				spellcheck="false"
				oninput={() => (autoName = false)}
			/>
		</Field>
		<Field label="Тип" group hint={editingId ? 'Тип нельзя изменить после создания.' : undefined}>
			<div class="choices">
				{#each kinds as kind (kind.value)}
					<label
						class="choice"
						class:selected={form.kind === kind.value}
						class:disabled={!!editingId}
					>
						<input
							type="radio"
							name="connection-kind"
							value={kind.value}
							checked={form.kind === kind.value}
							disabled={!!editingId}
							onchange={() => changeKind(kind.value)}
						/>
						<span class="choice-title">{kind.title}</span>
						<span class="choice-text">{kind.text}</span>
					</label>
				{/each}
			</div>
		</Field>
		<Field label="Сервер" hint="Сервер Xray для исходящего трафика. Можно изменить в любой момент.">
			<select bind:value={form.serverId}>
				{#each servers.data ?? [] as server (server.id)}
					<option value={server.id}>{server.name} — {server.protocol}</option>
				{/each}
			</select>
		</Field>
	</div>
	{#snippet actions()}
		<button class="btn" onclick={() => (editorOpen = false)}>Отмена</button>
		<button
			class="btn primary"
			disabled={!form.name.trim() || !form.serverId}
			aria-busy={saving}
			onclick={save}>{editingId ? 'Сохранить' : 'Создать соединение'}</button
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

	.live {
		display: inline-flex;
		align-items: center;
		gap: 8px;
		margin-left: auto;
	}

	.pulse {
		width: 7px;
		height: 7px;
		border-radius: 999px;
		background: var(--ok);
		animation: pulse 2s ease-in-out infinite;
	}

	@keyframes pulse {
		50% {
			opacity: 0.3;
		}
	}

	.name {
		color: var(--ink);
		font-weight: 500;
		text-align: left;
	}

	.name:hover {
		color: var(--accent-text);
	}

	.problem {
		display: flex;
		align-items: center;
		gap: 6px;
		margin-top: 4px;
		color: var(--danger);
	}
</style>
