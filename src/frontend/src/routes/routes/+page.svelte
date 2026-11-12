<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import { formatDateTime } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { label, refreshStatuses, routeStates } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { attempt } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type Route = components['schemas']['DnsRouteDto'];
	type ListSummary = components['schemas']['DomainListSummaryDto'];

	const routes = new Resource(() => unwrap(api.GET('/api/dns-routes')));
	const connections = new Resource(() => unwrap(api.GET('/api/connections')));
	const sources = new Resource(() => unwrap(api.GET('/api/domain-sources')));

	let editorOpen = $state(false);
	let editing = $state<Route>();
	let form = $state({
		sourceId: '',
		listName: '',
		attributeFilter: '',
		connectionId: '',
		exclusive: false
	});
	let catalog = $state<ListSummary[]>([]);
	let saving = $state(false);
	let applying = $state(false);
	let busy = $state<Record<string, boolean>>({});
	let catalogTimer: ReturnType<typeof setTimeout> | undefined;

	const selectedList = $derived(catalog.find((list) => list.name === form.listName));

	$effect(() => {
		const query = page.url.searchParams;
		const sourceId = query.get('source');
		if (!sourceId) return;
		openCreate({
			sourceId,
			listName: query.get('list') ?? '',
			attributeFilter: query.get('attrs') ?? ''
		});
		void goto(resolve('/routes'), { replaceState: true });
	});

	$effect(() => {
		if (!editorOpen || editing) return;
		if (!form.connectionId && connections.data?.length) form.connectionId = connections.data[0].id;
		if (!form.sourceId && sources.data?.length) {
			form.sourceId = sources.data[0].id;
			void loadCatalog();
		}
	});

	function openCreate(prefill: Partial<typeof form> = {}) {
		editing = undefined;
		form = {
			sourceId: prefill.sourceId ?? sources.data?.[0]?.id ?? '',
			listName: prefill.listName ?? '',
			attributeFilter: prefill.attributeFilter ?? '',
			connectionId: connections.data?.[0]?.id ?? '',
			exclusive: prefill.exclusive ?? false
		};
		editorOpen = true;
		void loadCatalog();
	}

	function openEdit(route: Route) {
		editing = route;
		form = {
			sourceId: route.sourceId,
			listName: route.listName,
			attributeFilter: route.attributeFilter ?? '',
			connectionId: route.connectionId,
			exclusive: route.exclusive
		};
		editorOpen = true;
		void loadCatalog();
	}

	async function loadCatalog() {
		if (!form.sourceId) return;
		const result = await attempt(() =>
			unwrap(
				api.GET('/api/domain-sources/{id}/lists', {
					params: {
						path: { id: form.sourceId },
						query: { query: form.listName.trim() || undefined }
					}
				})
			)
		);
		catalog = result ?? [];
	}

	function catalogSearch() {
		clearTimeout(catalogTimer);
		catalogTimer = setTimeout(() => void loadCatalog(), 250);
	}

	async function save() {
		saving = true;
		const attributeFilter = form.attributeFilter.trim() || null;
		const target = editing;
		const saved = await attempt(
			() =>
				target
					? unwrap(
							api.PUT('/api/dns-routes/{id}', {
								params: { path: { id: target.id } },
								body: {
									connectionId: form.connectionId,
									attributeFilter,
									exclusive: form.exclusive
								}
							})
						)
					: unwrap(
							api.POST('/api/dns-routes', {
								body: {
									sourceId: form.sourceId,
									listName: form.listName.trim(),
									connectionId: form.connectionId,
									attributeFilter,
									exclusive: form.exclusive
								}
							})
						),
			target ? 'Маршрут изменён' : 'Маршрут создан'
		);
		saving = false;
		if (!saved) return;
		editorOpen = false;
		void routes.reload();
	}

	async function toggle(route: Route) {
		busy[route.id] = true;
		const action = route.state === 'Disabled' ? 'enable' : 'disable';
		await attempt(
			() =>
				unwrap(api.POST(`/api/dns-routes/{id}/${action}`, { params: { path: { id: route.id } } })),
			action === 'enable' ? 'Правило включено' : 'Правило выключено'
		);
		busy[route.id] = false;
		void routes.reload();
	}

	async function remove(route: Route) {
		if (
			!confirm(
				`Удалить маршрут «${route.listName}» вместе с группой ${route.groupName} в Keenetic?`
			)
		)
			return;
		const done = await attempt(
			() => unwrap(api.DELETE('/api/dns-routes/{id}', { params: { path: { id: route.id } } })),
			'Маршрут удалён'
		);
		if (done !== undefined) void routes.reload();
	}

	async function applyAll() {
		applying = true;
		await trackJob(api.POST('/api/dns-routes/apply'));
		applying = false;
		void routes.reload();
	}
</script>

<div class="toolbar">
	<h1>DNS-маршруты</h1>
	<button class="secondary" disabled={applying} onclick={applyAll}>
		{applying ? 'Применяется…' : 'Применить все'}
	</button>
	<button onclick={() => openCreate()} disabled={!connections.data?.length}>Добавить</button>
</div>

<p class="muted intro">
	Маршрут — один список источника в одно соединение: группа <code>fg-…</code> в Keenetic и правило DNS-маршрута
	на интерфейс. Правило, выключенное в Keenetic, FlowGate сам не включит.
</p>

<Loadable resource={routes}>
	{#snippet children(list)}
		{#if list.length === 0}
			<p class="card empty">
				{connections.data?.length === 0 ? 'Сначала создайте соединение.' : 'Маршрутов нет.'}
			</p>
		{:else}
			<div class="table-wrap">
				<table>
					<thead>
						<tr>
							<th>Список</th>
							<th>Соединение</th>
							<th>Группа</th>
							<th>Правило</th>
							<th>Применение</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						{#each list as route (route.id)}
							{@const state = label(routeStates, route.state)}
							{@const apply = route.lastApply}
							{@const applyStatus = label(refreshStatuses, apply?.status)}
							<tr>
								<td>
									<code>{route.listName}</code>
									{#if route.attributeFilter}<Badge>@{route.attributeFilter}</Badge>{/if}
									{#if route.exclusive}<Badge>эксклюзивный</Badge>{/if}
									<div class="muted small">{route.sourceName ?? 'источник удалён'}</div>
								</td>
								<td>
									{#if route.connectionName}
										{route.connectionName}
										<div class="muted small">{route.interfaceName}</div>
									{:else}<Badge tone="danger">соединение удалено</Badge>{/if}
								</td>
								<td><code>{route.groupName}</code></td>
								<td><Badge tone={state.tone}>{state.label}</Badge></td>
								<td>
									{#if apply}
										<Badge tone={applyStatus.tone}>{applyStatus.label}</Badge>
										<div class="muted small">
											{apply.entries} эл.{Number(apply.parts) > 1 ? `, ${apply.parts} части` : ''} · {formatDateTime(
												apply.at
											)}
										</div>
										{#if apply.error}<div class="error small">{apply.error}</div>{/if}
										{#each apply.warnings as warning (warning)}
											<div class="muted small">{warning}</div>
										{/each}
									{:else}—{/if}
								</td>
								<td class="actions">
									{#if route.state !== 'Pending'}
										<button
											class="secondary small"
											disabled={busy[route.id]}
											onclick={() => toggle(route)}
										>
											{route.state === 'Disabled' ? 'Включить' : 'Выключить'}
										</button>
									{/if}
									<button class="secondary small" onclick={() => openEdit(route)}>Изменить</button>
									<button class="danger small" onclick={() => remove(route)}>Удалить</button>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}
	{/snippet}
</Loadable>

<Dialog bind:open={editorOpen} title={editing ? 'Маршрут' : 'Новый маршрут'}>
	<div class="form">
		<label>
			Источник
			<select bind:value={form.sourceId} disabled={!!editing} onchange={loadCatalog}>
				{#each sources.data ?? [] as source (source.id)}
					<option value={source.id}>{source.name}</option>
				{/each}
			</select>
		</label>
		<label>
			Список
			<input
				bind:value={form.listName}
				disabled={!!editing}
				list="route-lists"
				placeholder="youtube"
				oninput={catalogSearch}
			/>
			<datalist id="route-lists">
				{#each catalog.slice(0, 200) as item (item.name)}
					<option value={item.name}>{item.domains} доменов, {item.ipv4} IPv4</option>
				{/each}
			</datalist>
			{#if selectedList}
				<span class="hint">
					доменов {selectedList.domains}, IPv4 {selectedList.ipv4}, IPv6 {selectedList.ipv6}
					{#if selectedList.attributes.length}· атрибуты: {selectedList.attributes.join(', ')}{/if}
				</span>
			{/if}
		</label>
		<label>
			Фильтр атрибутов
			<input bind:value={form.attributeFilter} placeholder="необязательно: ads или !ads" />
		</label>
		<label>
			Соединение
			<select bind:value={form.connectionId}>
				{#each connections.data ?? [] as connection (connection.id)}
					<option value={connection.id}>{connection.name} — {connection.interfaceName}</option>
				{/each}
			</select>
		</label>
		<label class="check">
			<input type="checkbox" bind:checked={form.exclusive} /> Эксклюзивный маршрут
		</label>
		<span class="hint">
			Трафик к доменам списка идёт только через это соединение; если оно недоступно — блокируется
		</span>
	</div>
	{#snippet actions()}
		<button class="secondary" onclick={() => (editorOpen = false)}>Отмена</button>
		<button
			disabled={saving || !form.connectionId || !form.listName.trim() || !form.sourceId}
			onclick={save}
		>
			{editing ? 'Сохранить' : 'Создать'}
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
