<script lang="ts">
	import { untrack } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { formatDateTime, toNumber } from '#lib/format.js';
	import { hotkey } from '#lib/hotkeys.js';
	import { findRunningJob } from '#lib/jobs.js';
	import { followJob, trackJob } from '#lib/jobs.svelte.js';
	import { label, refreshStatuses, routeStates } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { matchesQuery, Selection, UrlParam } from '#lib/table.svelte.js';
	import { attempt, notify, notifyError } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import BulkBar from '#lib/components/BulkBar.svelte';
	import Checkbox from '#lib/components/Checkbox.svelte';
	import Copyable from '#lib/components/Copyable.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Field from '#lib/components/Field.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import ListPreview from '#lib/components/ListPreview.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import Menu from '#lib/components/Menu.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SearchInput from '#lib/components/SearchInput.svelte';
	import Switch from '#lib/components/Switch.svelte';

	type Route = components['schemas']['DnsRouteDto'];
	type ListSummary = components['schemas']['DomainListSummaryDto'];
	type StateFilter = '' | 'on' | 'off' | 'issues';

	const routes = new Resource(() => unwrap(api.GET('/api/dns-routes')), {
		live: ['dns-routes', 'connections', 'domain-sources']
	});
	const connections = new Resource(() => unwrap(api.GET('/api/connections')), {
		live: ['connections']
	});
	const sources = new Resource(() => unwrap(api.GET('/api/domain-sources')), {
		live: ['domain-sources']
	});

	const query = new UrlParam('q');
	const stateFilter = new UrlParam('state');
	const selection = new Selection();

	let editorOpen = $state(false);
	let editing = $state<Route>();
	let form = $state(emptyForm());
	let catalog = $state<ListSummary[]>([]);
	let catalogLoading = $state(false);
	let saving = $state(false);
	let applying = $state(false);
	let bulkBusy = $state(false);
	/** Переключения правил в процессе: id → состояние, к которому идём. */
	let pending = $state<Record<string, boolean>>({});
	let catalogTimer: ReturnType<typeof setTimeout> | undefined;
	let viewOpen = $state(false);
	let viewing = $state<Route>();

	const all = $derived(routes.data ?? []);
	const noConnections = $derived(connections.data?.length === 0);
	const selectedList = $derived(catalog.find((list) => list.name === form.listName.trim()));
	const counts = $derived({
		on: all.filter((route) => route.state === 'Enabled').length,
		off: all.filter((route) => route.state === 'Disabled').length,
		issues: all.filter(hasIssue).length
	});
	const visible = $derived(
		all.filter(
			(route) =>
				matchesState(route, stateFilter.value as StateFilter) &&
				matchesQuery(query.value, [
					route.listName,
					route.sourceName,
					route.connectionName,
					route.interfaceName,
					route.groupName,
					route.attributeFilter
				])
		)
	);
	const visibleIds = $derived(visible.map((route) => route.id));
	/** Источник показываем под списком, только если маршруты берут списки из разных источников. */
	const showSources = $derived(new Set(all.map((route) => route.sourceId)).size > 1);
	const selectedRoutes = $derived(all.filter((route) => selection.has(route.id)));

	$effect(() => {
		selection.retain(all.map((route) => route.id));
	});

	$effect(() => hotkey('n', () => !noConnections && openCreate()));

	// Переход из источника (?source=&list=&attrs=) или из палитры (?new=1) открывает форму создания.
	$effect(() => {
		const params = page.url.searchParams;
		const sourceId = params.get('source');
		if (!sourceId && params.get('new') !== '1') return;
		const prefill = {
			sourceId: sourceId ?? undefined,
			listName: params.get('list') ?? '',
			attributeFilter: params.get('attrs') ?? ''
		};
		untrack(() => {
			openCreate(prefill);
			void goto(resolve('/routes'), { replace: true, reset: false });
		});
	});

	$effect(() => {
		if (!editorOpen || editing) return;
		if (!form.connectionId && connections.data?.length) form.connectionId = connections.data[0].id;
		if (!form.sourceId && sources.data?.length) {
			form.sourceId = sources.data[0].id;
			void loadCatalog();
		}
	});

	function emptyForm() {
		return {
			sourceId: '',
			listName: '',
			attributeFilter: '',
			connectionId: '',
			exclusive: false
		};
	}

	function hasIssue(route: Route): boolean {
		return (
			route.state === 'Pending' ||
			!route.connectionName ||
			!route.sourceName ||
			(!!route.lastApply && route.lastApply.status !== 'Ok')
		);
	}

	function matchesState(route: Route, filter: StateFilter): boolean {
		if (filter === 'on') return route.state === 'Enabled';
		if (filter === 'off') return route.state === 'Disabled';
		if (filter === 'issues') return hasIssue(route);
		return true;
	}

	function openCreate(prefill: Partial<ReturnType<typeof emptyForm>> = {}) {
		editing = undefined;
		form = {
			...emptyForm(),
			sourceId: prefill.sourceId ?? sources.data?.[0]?.id ?? '',
			listName: prefill.listName ?? '',
			attributeFilter: prefill.attributeFilter ?? '',
			connectionId: connections.data?.[0]?.id ?? ''
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

	function openView(route: Route) {
		viewing = route;
		viewOpen = true;
	}

	async function loadCatalog() {
		if (!form.sourceId) return;
		catalogLoading = true;
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
		catalogLoading = false;
		catalog = result ?? [];
	}

	function catalogSearch() {
		clearTimeout(catalogTimer);
		catalogTimer = setTimeout(() => void loadCatalog(), 250);
	}

	async function save() {
		if (!form.connectionId || !form.listName.trim() || !form.sourceId) return;
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
			target
				? 'Маршрут сохранён. Выполняется применение в Keenetic'
				: 'Маршрут создан. Выполняется применение в Keenetic'
		);
		saving = false;
		if (!saved) return;
		editorOpen = false;
		await afterChange();
	}

	/** Показывает сохранённое намерение сразу и обновляет таблицу, когда фоновое применение в Keenetic закончится. */
	async function afterChange() {
		void routes.reload();
		const job = await findRunningJob('dns-routes-apply').catch(() => undefined);
		if (!job) return;
		await followJob(job);
		await routes.reload();
	}

	async function setEnabled(route: Route, enabled: boolean): Promise<boolean> {
		const action = enabled ? 'enable' : 'disable';
		try {
			await unwrap(
				api.POST(`/api/dns-routes/{id}/${action}`, { params: { path: { id: route.id } } })
			);
			return true;
		} catch (error) {
			notifyError(error);
			return false;
		}
	}

	async function toggle(route: Route) {
		const target = route.state === 'Disabled';
		pending[route.id] = target;
		const done = await setEnabled(route, target);
		if (done) await afterChange();
		delete pending[route.id];
	}

	async function bulkSetEnabled(enabled: boolean) {
		const list = selectedRoutes.filter(
			(route) => route.state !== 'Pending' && (route.state === 'Enabled') !== enabled
		);
		if (list.length === 0) {
			notify(
				'info',
				enabled ? 'Выбранные правила уже включены' : 'Выбранные правила уже выключены'
			);
			return;
		}
		bulkBusy = true;
		for (const route of list) pending[route.id] = enabled;
		let done = 0;
		for (const route of list) if (await setEnabled(route, enabled)) done++;
		if (done > 0) notify('ok', `${enabled ? 'Включено' : 'Выключено'} правил: ${done}`);
		await afterChange();
		for (const route of list) delete pending[route.id];
		bulkBusy = false;
	}

	async function removeRoutes(list: Route[]) {
		const one = list.length === 1;
		const ok = await ask({
			title: one ? `Удалить маршрут «${list[0].listName}»?` : `Удалить маршруты: ${list.length}?`,
			message: one
				? `Группа ${list[0].groupName} и правило будут удалены из Keenetic. Трафик к доменам списка пойдёт напрямую.`
				: 'Группы и правила будут удалены из Keenetic. Трафик к доменам этих списков пойдёт напрямую.',
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		bulkBusy = true;
		let done = 0;
		for (const route of list) {
			try {
				await unwrap(api.DELETE('/api/dns-routes/{id}', { params: { path: { id: route.id } } }));
				selection.ids.delete(route.id);
				done++;
			} catch (error) {
				notifyError(error);
			}
		}
		if (done > 0) notify('ok', one ? 'Маршрут удалён' : `Удалено маршрутов: ${done}`);
		bulkBusy = false;
		await afterChange();
	}

	async function applyAll() {
		applying = true;
		await trackJob(api.POST('/api/dns-routes/apply'));
		applying = false;
		void routes.reload();
	}

	function partsLabel(parts: number | string): string {
		const count = toNumber(parts) ?? 1;
		return count > 1 ? `, ${count} части` : '';
	}
</script>

<PageHeader
	title="DNS-маршруты"
	description="Маршрут направляет трафик к доменам списка через соединение. В Keenetic создаются группа доменов и правило DNS-маршрута."
>
	{#snippet actions()}
		<button
			class="btn"
			aria-busy={applying}
			onclick={applyAll}
			title="Синхронизировать группы и правила с Keenetic"
		>
			<Icon name="refresh" size={16} />Синхронизировать
		</button>
		<button
			class="btn primary"
			disabled={noConnections}
			onclick={() => openCreate()}
			title="Клавиша N"
		>
			<Icon name="plus" size={16} />Новый маршрут
		</button>
	{/snippet}
</PageHeader>

<Loadable resource={routes}>
	{#snippet children(list)}
		{#if list.length === 0}
			{#if noConnections}
				<EmptyState
					icon="plug"
					title="Нет соединений"
					text="Для создания маршрута требуется соединение."
				>
					<a class="btn primary" href={resolve('/connections')}>К соединениям</a>
				</EmptyState>
			{:else}
				<EmptyState
					icon="route"
					title="Нет маршрутов"
					text="Маршрут связывает список доменов из источника с соединением."
				>
					<button class="btn primary" onclick={() => openCreate()}>Создать маршрут</button>
					<a class="btn" href={resolve('/sources')}>Источники списков</a>
				</EmptyState>
			{/if}
		{:else}
			<div class="toolbar">
				<SearchInput
					value={query.value}
					placeholder="Список, соединение или группа"
					oninput={(value) => query.set(value)}
				/>
				<div class="segmented" role="group" aria-label="Состояние правила">
					<button aria-pressed={stateFilter.value === ''} onclick={() => stateFilter.set('')}>
						Все <span class="faint num">{list.length}</span>
					</button>
					<button aria-pressed={stateFilter.value === 'on'} onclick={() => stateFilter.set('on')}>
						Включены <span class="faint num">{counts.on}</span>
					</button>
					<button aria-pressed={stateFilter.value === 'off'} onclick={() => stateFilter.set('off')}>
						Выключены <span class="faint num">{counts.off}</span>
					</button>
					{#if counts.issues > 0}
						<button
							class="issues"
							aria-pressed={stateFilter.value === 'issues'}
							onclick={() => stateFilter.set('issues')}
						>
							С ошибками <span class="num">{counts.issues}</span>
						</button>
					{/if}
				</div>
			</div>

			<div class="panel">
				<div class="table-scroll">
					<table class="table">
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
								<th class="switch-cell"><span class="visually-hidden">Правило</span></th>
								<th>Список</th>
								<th>Соединение</th>
								<th>Группа Keenetic</th>
								<th>Применение</th>
								<th class="actions-cell"><span class="visually-hidden">Действия</span></th>
							</tr>
						</thead>
						<tbody>
							{#each visible as route (route.id)}
								{@const apply = route.lastApply}
								{@const applyStatus = label(refreshStatuses, apply?.status)}
								{@const routeState = label(routeStates, route.state)}
								{@const enabled = pending[route.id] ?? route.state === 'Enabled'}
								<tr class:selected={selection.has(route.id)} class:off={route.state === 'Disabled'}>
									<td class="check-cell">
										<Checkbox
											label="Выбрать {route.listName}"
											checked={selection.has(route.id)}
											onchange={() => selection.toggle(route.id)}
										/>
									</td>
									<td class="switch-cell">
										<Switch
											checked={enabled}
											busy={route.id in pending}
											disabled={route.state === 'Pending'}
											label="Правило {route.listName}"
											title={route.state === 'Pending'
												? 'Правило ещё не создано в Keenetic'
												: enabled
													? 'Выключить правило'
													: 'Включить правило'}
											onchange={() => toggle(route)}
										/>
									</td>
									<td>
										<button
											class="list"
											title="Источник: {route.sourceName ?? 'удалён'}"
											onclick={() => openEdit(route)}
										>
											{route.listName}{#if route.attributeFilter}<span class="attr"
													>@{route.attributeFilter}</span
												>{/if}
										</button>
										{#if route.state === 'Pending' || !route.sourceName || showSources}
											<div class="sub">
												{#if route.state === 'Pending'}<span class="pending"
														>{routeState.label}</span
													>{/if}
												{#if !route.sourceName}<span class="error-text">источник удалён</span>
												{:else if showSources}{route.state === 'Pending'
														? ' · '
														: ''}{route.sourceName}{/if}
											</div>
										{/if}
									</td>
									<td>
										{#if route.connectionName}
											<span class="conn" title="Интерфейс Keenetic: {route.interfaceName ?? '—'}">
												{route.connectionName}
												{#if route.exclusive}
													<Badge
														tone="accent"
														title="При недоступности соединения трафик блокируется, а не идёт напрямую"
														>эксклюзивный</Badge
													>
												{/if}
											</span>
										{:else}
											<Badge tone="danger">соединение удалено</Badge>
										{/if}
									</td>
									<td><Copyable value={route.groupName} /></td>
									<td>
										{#if apply}
											<Badge tone={applyStatus.tone} dot>{applyStatus.label}</Badge>
											<div class="sub" title="Последняя синхронизация: {formatDateTime(apply.at)}">
												{apply.entries} эл.{partsLabel(apply.parts)} · изменён
												{apply.contentChangedAt
													? formatDateTime(apply.contentChangedAt)
													: 'нет данных'}
											</div>
											{#if apply.error}<div class="error-text small">{apply.error}</div>{/if}
											{#each apply.warnings as warning (warning)}
												<div class="sub">{warning}</div>
											{/each}
										{:else}
											<span class="faint">не применялся</span>
										{/if}
									</td>
									<td class="actions-cell">
										<Menu
											ariaLabel="Действия с маршрутом {route.listName}"
											items={[
												{ label: 'Изменить', icon: 'edit', onclick: () => openEdit(route) },
												{
													label: 'Состав списка',
													icon: 'eye',
													disabled: !route.sourceName,
													onclick: () => openView(route)
												},
												{
													label: 'Открыть источник',
													icon: 'layers',
													disabled: !route.sourceName,
													href: resolve('/sources/[id]', { id: route.sourceId })
												},
												'separator',
												{
													label: 'Удалить',
													icon: 'trash',
													danger: true,
													onclick: () => removeRoutes([route])
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
											text="Нет маршрутов, соответствующих условиям."
										>
											<button
												class="btn"
												onclick={() => {
													query.set('');
													stateFilter.set('');
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
			<p class="muted small note">
				Правила, отключённые вручную в Keenetic, автоматически не включаются.
			</p>
		{/if}
	{/snippet}
</Loadable>

<BulkBar count={selection.size} onclear={() => selection.clear()}>
	<button class="btn sm" disabled={bulkBusy} onclick={() => bulkSetEnabled(true)}>Включить</button>
	<button class="btn sm" disabled={bulkBusy} onclick={() => bulkSetEnabled(false)}>Выключить</button
	>
	<button class="btn sm danger" aria-busy={bulkBusy} onclick={() => removeRoutes(selectedRoutes)}>
		<Icon name="trash" size={14} />Удалить
	</button>
</BulkBar>

<Dialog
	bind:open={viewOpen}
	title="Список {viewing?.listName ?? ''}"
	description="Элементы, передаваемые в Keenetic"
	size="lg"
>
	{#if viewing}
		<ListPreview
			sourceId={viewing.sourceId}
			listName={viewing.listName}
			attrs={viewing.attributeFilter ?? ''}
			editableFilter={false}
		/>
	{/if}
	{#snippet actions()}
		<button class="btn" onclick={() => (viewOpen = false)}>Закрыть</button>
	{/snippet}
</Dialog>

<Dialog
	bind:open={editorOpen}
	variant="side"
	title={editing ? `Маршрут ${editing.listName}` : 'Новый маршрут'}
	description={editing
		? 'Список и источник изменить нельзя. Для другого списка создайте новый маршрут.'
		: 'Список доменов и соединение для маршрутизации.'}
	onsubmit={save}
>
	<div class="form">
		<div class="step">
			<span class="step-title">Список доменов</span>
			<Field label="Источник">
				<select bind:value={form.sourceId} disabled={!!editing} onchange={loadCatalog}>
					{#each sources.data ?? [] as source (source.id)}
						<option value={source.id}>{source.name}</option>
					{/each}
				</select>
			</Field>
			<Field
				label="Список"
				hint={editing ? undefined : 'Варианты подставляются из каталога источника.'}
			>
				<input
					bind:value={form.listName}
					disabled={!!editing}
					list="route-lists"
					placeholder="youtube"
					spellcheck="false"
					autocomplete="off"
					oninput={catalogSearch}
				/>
				<datalist id="route-lists">
					{#each catalog.slice(0, 200) as item (item.name)}
						<option value={item.name}>{item.domains} доменов, {item.ipv4} IPv4</option>
					{/each}
				</datalist>
				{#snippet extra()}
					{#if selectedList}
						<div class="list-info">
							<span><b class="num">{selectedList.domains}</b> доменов</span>
							<span><b class="num">{selectedList.ipv4}</b> IPv4</span>
							<span><b class="num">{selectedList.ipv6}</b> IPv6</span>
							{#if selectedList.attributes.length}
								<span class="muted"
									>атрибуты: {selectedList.attributes.map((attr) => `@${attr}`).join(' ')}</span
								>
							{/if}
						</div>
					{:else if form.listName.trim() && !catalogLoading && !editing}
						<span class="hint warn-text">Список не найден в источнике.</span>
					{/if}
				{/snippet}
			</Field>
			<Field
				label="Фильтр атрибутов"
				hint="Необязательно. ads — только строки с @ads, !ads — все, кроме них."
			>
				<input bind:value={form.attributeFilter} placeholder="без фильтра" spellcheck="false" />
			</Field>
		</div>

		<div class="step">
			<span class="step-title">Соединение</span>
			<div class="choices">
				{#each connections.data ?? [] as connection (connection.id)}
					<label class="choice" class:selected={form.connectionId === connection.id}>
						<input type="radio" bind:group={form.connectionId} value={connection.id} />
						<span class="choice-title">{connection.name}</span>
						<span class="choice-text">
							{connection.serverName ?? 'сервер удалён'} ·
							<span class="mono">{connection.interfaceName}</span>
						</span>
					</label>
				{/each}
			</div>
			<label class="check">
				<input type="checkbox" bind:checked={form.exclusive} />
				<span>
					Эксклюзивный маршрут
					<span class="hint"
						>Трафик направляется только через выбранное соединение. При его недоступности трафик
						блокируется.</span
					>
				</span>
			</label>
		</div>
	</div>
	{#snippet actions()}
		<button class="btn" onclick={() => (editorOpen = false)}>Отмена</button>
		<button
			class="btn primary"
			disabled={!form.connectionId || !form.listName.trim() || !form.sourceId}
			aria-busy={saving}
			onclick={save}>{editing ? 'Сохранить' : 'Создать маршрут'}</button
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

	.segmented .issues {
		color: var(--warn);
	}

	.list {
		color: var(--ink);
		font-size: 14.5px;
		font-weight: 600;
		text-align: left;
	}

	.list:hover {
		color: var(--accent-text);
	}

	.attr {
		margin-left: 6px;
		color: var(--muted);
		font-family: var(--mono);
		font-size: 12px;
		font-weight: 400;
	}

	.conn {
		display: inline-flex;
		align-items: center;
		gap: 6px;
		color: var(--ink-2);
	}

	tr.off .list,
	tr.off .conn {
		color: var(--faint);
	}

	/* Переключатель правила — сразу за чекбоксом, без своей подписи: состояние видно по строке. */
	.switch-cell {
		width: 1%;
		padding-left: 4px;
		padding-right: 4px;
	}

	.pending {
		color: var(--warn);
		font-weight: 500;
	}

	.note {
		margin-top: 12px;
	}

	.step {
		display: grid;
		gap: 16px;
		padding-bottom: 20px;
		border-bottom: 1px solid var(--line);
	}

	.step:last-child {
		padding-bottom: 0;
		border-bottom: 0;
	}

	.step-title {
		font-weight: 600;
	}

	.list-info {
		display: flex;
		gap: 4px 14px;
		flex-wrap: wrap;
		padding: 8px 10px;
		border-radius: var(--r-sm);
		background: var(--surface-2);
		font-size: 12.5px;
	}

	.warn-text {
		color: var(--warn);
		font-size: 12.5px;
	}
</style>
