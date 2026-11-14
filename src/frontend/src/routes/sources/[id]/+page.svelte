<script lang="ts">
	import { untrack } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { onChange } from '#lib/events.js';
	import { formatDateTime, toNumber } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { label, refreshStatuses, sourceKinds } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { formToRequest, sourceOrigin, sourceToForm, type SourceForm } from '#lib/sources.js';
	import { attempt, attemptOk } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import Copyable from '#lib/components/Copyable.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Field from '#lib/components/Field.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import ListFormatHelp from '#lib/components/ListFormatHelp.svelte';
	import ListPreview from '#lib/components/ListPreview.svelte';
	import Menu from '#lib/components/Menu.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SearchInput from '#lib/components/SearchInput.svelte';
	import SourceFormFields from '#lib/components/SourceForm.svelte';
	import UpdateButton from '#lib/components/UpdateButton.svelte';

	type Source = components['schemas']['DomainSourceDto'];
	type ListSummary = components['schemas']['DomainListSummaryDto'];
	type LocalList = components['schemas']['LocalListDto'];
	type UpdateVia = components['schemas']['UpdateVia'];

	const visibleLimit = 300;
	const id = $derived(page.params.id ?? '');

	const routes = new Resource(() => unwrap(api.GET('/api/dns-routes')), { live: ['dns-routes'] });

	let source = $state<Source>();
	let loadError = $state<unknown>();
	let lists = $state<ListSummary[]>();
	let listsError = $state<unknown>();
	let query = $state('');
	let localLists = $state<LocalList[]>([]);
	let refreshing = $state(false);

	let editOpen = $state(false);
	let editForm = $state<SourceForm>(
		sourceToForm({ name: '', kind: 'Local', autoUpdate: false } as Source)
	);
	let saving = $state(false);

	let previewOpen = $state(false);
	let previewName = $state('');
	let previewAttrs = $state('');

	let localOpen = $state(false);
	let localIsNew = $state(false);
	let localName = $state('');
	let localText = $state('');
	let localOriginal = $state('');
	let localSaving = $state(false);

	let searchTimer: ReturnType<typeof setTimeout> | undefined;

	/** Списки этого источника, уже направленные в маршруты: имя списка → число маршрутов. */
	const routed = $derived.by(() => {
		// Временная карта внутри $derived: реактивность ей не нужна.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const map = new Map<string, number>();
		for (const route of routes.data ?? []) {
			if (route.sourceId === id) map.set(route.listName, (map.get(route.listName) ?? 0) + 1);
		}
		return map;
	});

	const localNameError = $derived(
		localIsNew && localName && !/^[a-z0-9._!-]+$/.test(localName)
			? 'Только латиница в нижнем регистре, цифры и - . _ !'
			: undefined
	);
	const localDirty = $derived(localIsNew ? !!localName.trim() : localText !== localOriginal);

	$effect(() => {
		const current = id;
		untrack(() => void loadAll(current));
	});

	$effect(() => onChange(['domain-sources', 'local-lists'], () => void loadAll(id)));

	async function loadAll(current: string) {
		try {
			source = await unwrap(
				api.GET('/api/domain-sources/{id}', { params: { path: { id: current } } })
			);
			loadError = undefined;
		} catch (error) {
			loadError = error;
			return;
		}
		await Promise.all([loadLists(), loadLocal()]);
	}

	async function loadLists() {
		try {
			lists = await unwrap(
				api.GET('/api/domain-sources/{id}/lists', {
					params: { path: { id }, query: { query: query.trim() || undefined } }
				})
			);
			listsError = undefined;
		} catch (error) {
			listsError = error;
		}
	}

	async function loadLocal() {
		if (source?.kind !== 'Local') return;
		const result = await attempt(() =>
			unwrap(api.GET('/api/domain-sources/{id}/local-lists', { params: { path: { id } } }))
		);
		if (result) localLists = result;
	}

	function search(value: string) {
		query = value;
		clearTimeout(searchTimer);
		searchTimer = setTimeout(() => void loadLists(), 250);
	}

	async function refresh(via: UpdateVia) {
		refreshing = true;
		await trackJob(
			api.POST('/api/domain-sources/{id}/refresh', { params: { path: { id }, query: { via } } })
		);
		refreshing = false;
		await loadAll(id);
	}

	function openEdit() {
		if (source) editForm = sourceToForm(source);
		editOpen = true;
	}

	async function saveEdit() {
		if (!editForm.name.trim()) return;
		saving = true;
		const result = await attempt(
			() =>
				unwrap(
					api.PUT('/api/domain-sources/{id}', {
						params: { path: { id } },
						body: formToRequest(editForm)
					})
				),
			'Источник сохранён'
		);
		saving = false;
		if (!result) return;
		source = result;
		editOpen = false;
	}

	async function remove() {
		if (!source) return;
		const ok = await ask({
			title: `Удалить источник «${source.name}»?`,
			message:
				Number(source.routeCount) > 0
					? `Маршруты, использующие списки источника (${source.routeCount}), перестанут обновляться.`
					: 'Загруженные списки будут удалены.',
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		const done = await attemptOk(
			() => unwrap(api.DELETE('/api/domain-sources/{id}', { params: { path: { id } } })),
			'Источник удалён'
		);
		if (done) await goto(resolve('/sources'));
	}

	function openPreview(name: string, attrs = '') {
		previewName = name;
		previewAttrs = attrs;
		previewOpen = true;
	}

	function routeHref(name: string, attrs: string): string {
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- одноразовая сборка query
		const params = new URLSearchParams({ source: id, list: name });
		if (attrs.trim()) params.set('attrs', attrs.trim());
		return resolve(`/routes?${params}`);
	}

	function openLocal(list?: LocalList) {
		localIsNew = !list;
		localName = list?.name ?? '';
		localText = list?.text ?? '';
		localOriginal = localText;
		localOpen = true;
	}

	async function saveLocal() {
		const name = localName.trim();
		if (!name || localNameError) return;
		localSaving = true;
		const result = await attempt(
			() =>
				unwrap(
					api.PUT('/api/domain-sources/{id}/local-lists/{name}', {
						params: { path: { id, name } },
						body: { text: localText }
					})
				),
			localIsNew ? `Список ${name} создан` : `Список ${name} сохранён`
		);
		localSaving = false;
		if (!result) return;
		localOpen = false;
		await Promise.all([loadLocal(), loadLists()]);
	}

	async function closeLocal() {
		if (localDirty) {
			const ok = await ask({
				title: 'Закрыть без сохранения?',
				message: 'Несохранённые изменения будут потеряны.',
				action: 'Закрыть',
				danger: true
			});
			if (!ok) return;
		}
		localOpen = false;
	}

	async function removeLocal(name: string) {
		const ok = await ask({
			title: `Удалить список «${name}»?`,
			message: routed.has(name)
				? 'Маршруты с этим списком перестанут обновляться.'
				: 'Действие необратимо.',
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		const done = await attemptOk(
			() =>
				unwrap(
					api.DELETE('/api/domain-sources/{id}/local-lists/{name}', {
						params: { path: { id, name } }
					})
				),
			'Список удалён'
		);
		if (done) await Promise.all([loadLocal(), loadLists()]);
	}

	function lineCount(text: string): number {
		return text.split('\n').filter((line) => line.trim() && !line.trim().startsWith('#')).length;
	}
</script>

<PageHeader
	title={source?.name ?? 'Источник'}
	back={{ href: resolve('/sources'), label: 'Источники списков' }}
>
	{#snippet meta()}
		{#if source}
			<Badge>{sourceKinds[source.kind] ?? source.kind}</Badge>
			{#if source.builtIn}<Badge tone="accent">встроенный</Badge>{/if}
		{/if}
	{/snippet}
	{#snippet actions()}
		{#if source}
			{#if source.kind !== 'Local'}
				<UpdateButton label="Обновить сейчас" busy={refreshing} onrun={refresh} />
			{/if}
			{#if !source.builtIn}
				<Menu
					small={false}
					buttonClass=""
					items={[
						{ label: 'Изменить источник', icon: 'edit', onclick: openEdit },
						'separator',
						{ label: 'Удалить источник', icon: 'trash', danger: true, onclick: remove }
					]}
				/>
			{/if}
		{/if}
	{/snippet}
</PageHeader>

{#if loadError}
	<EmptyState icon="alert" title="Не удалось загрузить источник" text={errorMessage(loadError)}>
		<button class="btn" onclick={() => loadAll(id)}>Повторить</button>
		<a class="btn" href={resolve('/sources')}>К источникам</a>
	</EmptyState>
{:else if !source}
	<div class="panel loading" aria-busy="true">Загрузка…</div>
{:else}
	{@const last = source.lastRefresh}
	{@const status = label(refreshStatuses, last?.status)}
	<div class="stack">
		<div class="facts panel">
			<div class="wide">
				<span class="fact-label">Откуда</span>
				<span class="fact-value mono small">{sourceOrigin(source)}</span>
			</div>
			{#if source.revision}
				<div>
					<span class="fact-label">Версия</span>
					<span class="fact-value"
						><Copyable value={source.revision} text={source.revision.slice(0, 12)} /></span
					>
				</div>
			{/if}
			<div>
				<span class="fact-label">Списков</span>
				<span class="fact-value num">{source.listCount}</span>
			</div>
			<div>
				<span class="fact-label">Маршрутов</span>
				<span class="fact-value num">{source.routeCount}</span>
			</div>
			<div>
				<span class="fact-label">Обновление</span>
				<span class="fact-value">
					{#if last}<Badge tone={status.tone} dot>{status.label}</Badge>{/if}
					<span class="muted small">
						{last
							? formatDateTime(last.at)
							: source.kind === 'Local'
								? 'вручную'
								: 'не выполнялось'}
						{#if source.kind !== 'Local'}· {source.autoUpdate ? 'по расписанию' : 'вручную'}{/if}
					</span>
				</span>
			</div>
		</div>

		{#if last?.error}
			<div class="banner danger" role="alert">
				<Icon name="alert" size={18} /><span>{last.error}</span>
			</div>
		{/if}
		{#if last && last.errors.length > 0}
			<details class="panel issues">
				<summary><Icon name="alert" size={16} />Ошибки в списках: {last.errors.length}</summary>
				<ul class="error-text small">
					{#each last.errors as error (error)}<li>{error}</li>{/each}
				</ul>
			</details>
		{/if}

		{#if source.kind === 'Local'}
			<section class="panel">
				<div class="panel-head">
					<h2>Локальные списки</h2>
					<button class="btn sm primary" onclick={() => openLocal()}>
						<Icon name="plus" size={14} />Новый список
					</button>
				</div>
				{#if localLists.length === 0}
					<EmptyState
						bare
						icon="list"
						title="Нет списков"
						text="Создайте список доменов и IP-адресов для использования в маршрутах."
					>
						<button class="btn primary" onclick={() => openLocal()}>Создать список</button>
					</EmptyState>
				{:else}
					<ul class="locals">
						{#each localLists as list (list.name)}
							<li>
								<button class="local-name" onclick={() => openLocal(list)}>
									<Icon name="list" size={16} />
									<span class="mono">{list.name}</span>
								</button>
								<span class="muted small num">{lineCount(list.text)} строк</span>
								<span class="muted small">изменён {formatDateTime(list.updatedAt)}</span>
								<span class="row end">
									<button class="btn sm" onclick={() => openLocal(list)}>Изменить</button>
									<Menu
										ariaLabel="Действия со списком {list.name}"
										items={[
											{
												label: 'Состав',
												icon: 'eye',
												onclick: () => openPreview(list.name)
											},
											{
												label: 'Создать маршрут',
												icon: 'route',
												href: routeHref(list.name, '')
											},
											'separator',
											{
												label: 'Удалить',
												icon: 'trash',
												danger: true,
												onclick: () => removeLocal(list.name)
											}
										]}
									/>
								</span>
							</li>
						{/each}
					</ul>
				{/if}
			</section>
		{/if}

		<section class="panel">
			<div class="panel-head">
				<h2>Каталог списков</h2>
				<SearchInput value={query} placeholder="Имя списка" label="Поиск списка" oninput={search} />
			</div>
			{#if listsError}
				<div class="panel-body error-text">{errorMessage(listsError)}</div>
			{:else if !source.downloaded}
				<EmptyState
					bare
					icon="download"
					title="Источник не загружен"
					text="Загрузка выполняется или завершилась с ошибкой. Состояние указано выше."
				/>
			{:else if lists === undefined}
				<div class="panel-body muted">Загрузка…</div>
			{:else if lists.length === 0}
				<EmptyState
					bare
					icon="search"
					title={query ? 'Ничего не найдено' : 'В источнике нет списков'}
				/>
			{:else}
				<div class="table-scroll">
					<table class="table cards">
						<thead>
							<tr>
								<th>Список</th>
								<th class="num">Доменов</th>
								<th class="num">IPv4</th>
								<th class="num">IPv6</th>
								<th class="num">Пропущено</th>
								<th>Атрибуты</th>
								<th class="actions-cell"><span class="visually-hidden">Действия</span></th>
							</tr>
						</thead>
						<tbody>
							{#each lists.slice(0, visibleLimit) as list (list.name)}
								{@const used = routed.get(list.name)}
								<tr>
									<td class="primary">
										<button class="list-name mono" onclick={() => openPreview(list.name)}
											>{list.name}</button
										>
										{#if used}
											<a href={resolve(`/routes?q=${encodeURIComponent(list.name)}`)}>
												<Badge tone="ok">в маршруте{used > 1 ? ` ×${used}` : ''}</Badge>
											</a>
										{/if}
									</td>
									<td data-label="Доменов" class="num">{list.domains}</td>
									<td data-label="IPv4" class="num">{list.ipv4}</td>
									<td data-label="IPv6" class="num">{list.ipv6}</td>
									<td
										data-label="Пропущено"
										class="num"
										class:faint={(toNumber(list.skipped) ?? 0) === 0}>{list.skipped}</td
									>
									<td data-label="Метки" class="attrs">
										{#each list.attributes as attr (attr)}
											<button
												class="attr"
												title="Состав только с @{attr}"
												onclick={() => openPreview(list.name, attr)}
											>
												@{attr}
											</button>
										{/each}
									</td>
									<td class="actions-cell">
										<div class="row end nowrap">
											<button class="btn sm ghost" onclick={() => openPreview(list.name)}>
												<Icon name="eye" size={14} />Состав
											</button>
											<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- адрес уже собран через resolve() -->
											<a class="btn sm" href={routeHref(list.name, '')}>
												<Icon name="route" size={14} />Создать маршрут
											</a>
										</div>
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
				{#if lists.length > visibleLimit}
					<p class="panel-body muted small">
						Показаны первые {visibleLimit} из {lists.length}. Уточните запрос.
					</p>
				{/if}
			{/if}
		</section>

		{#if source.kind === 'Local'}<ListFormatHelp />{/if}
	</div>
{/if}

<Dialog
	bind:open={previewOpen}
	title="Список {previewName}"
	description="Элементы, передаваемые в Keenetic"
	size="lg"
>
	<ListPreview sourceId={id} listName={previewName} bind:attrs={previewAttrs} />
	{#snippet actions()}
		<button class="btn" onclick={() => (previewOpen = false)}>Закрыть</button>
		<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- адрес уже собран через resolve() -->
		<a class="btn primary" href={routeHref(previewName, previewAttrs)}>
			<Icon name="route" size={16} />Создать маршрут
		</a>
	{/snippet}
</Dialog>

<Dialog bind:open={editOpen} variant="side" title="Источник" onsubmit={saveEdit}>
	<SourceFormFields bind:form={editForm} editing={true} />
	{#snippet actions()}
		<button class="btn" onclick={() => (editOpen = false)}>Отмена</button>
		<button
			class="btn primary"
			disabled={!editForm.name.trim()}
			aria-busy={saving}
			onclick={saveEdit}
		>
			Сохранить
		</button>
	{/snippet}
</Dialog>

<Dialog
	bind:open={localOpen}
	variant="side"
	size="lg"
	title={localIsNew ? 'Новый список' : `Список ${localName}`}
	description="Домены, подсети и include — по одному в строке."
	onsubmit={saveLocal}
>
	<div class="form">
		{#if localIsNew}
			<Field
				label="Имя списка"
				hint="Латиница в нижнем регистре, цифры, - . _ !"
				error={localNameError}
			>
				<input bind:value={localName} placeholder="telegram" maxlength="64" spellcheck="false" />
			</Field>
		{/if}
		<Field label="Строки" hint="{lineCount(localText)} значимых строк">
			<textarea
				class="local-text"
				bind:value={localText}
				rows="18"
				spellcheck="false"
				placeholder="telegram.org&#10;t.me&#10;91.108.4.0/22&#10;# комментарий"></textarea>
		</Field>
		<ListFormatHelp />
	</div>
	{#snippet actions()}
		<button class="btn" onclick={closeLocal}>Отмена</button>
		<button
			class="btn primary"
			disabled={!localName.trim() || !!localNameError}
			aria-busy={localSaving}
			onclick={saveLocal}>{localIsNew ? 'Создать список' : 'Сохранить'}</button
		>
	{/snippet}
</Dialog>

<style>
	.loading {
		padding: 24px;
		color: var(--muted);
	}

	.facts {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
	}

	.facts > div {
		display: grid;
		gap: 4px;
		align-content: start;
		padding: 16px 20px;
		border-left: 1px solid var(--line);
	}

	.facts > div:first-child {
		border-left: 0;
	}

	.facts .wide {
		grid-column: span 2;
		overflow-wrap: anywhere;
	}

	.fact-label {
		color: var(--muted);
		font-size: 12.5px;
	}

	.facts > div {
		min-width: 0;
	}

	.fact-value {
		min-width: 0;
		overflow-wrap: anywhere;
		display: flex;
		align-items: center;
		gap: 8px;
		flex-wrap: wrap;
		font-weight: 500;
	}

	.banner {
		display: flex;
		align-items: center;
		gap: 12px;
		padding: 12px 16px;
		border-radius: var(--r-md);
		background: var(--danger-soft);
		color: var(--ink-2);
	}

	.banner :global(.icon) {
		color: var(--danger);
	}

	.issues summary {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 12px 16px;
		color: var(--danger);
		cursor: pointer;
	}

	.issues ul {
		margin: 0;
		padding: 0 20px 16px 40px;
	}

	.locals {
		margin: 0;
		padding: 0;
		list-style: none;
	}

	.locals li {
		display: grid;
		grid-template-columns: minmax(160px, 1fr) auto auto auto;
		align-items: center;
		gap: 20px;
		padding: 10px 14px 10px 20px;
		border-top: 1px solid var(--line);
	}

	.locals li:first-child {
		border-top: 0;
	}

	.local-name,
	.list-name {
		display: inline-flex;
		align-items: center;
		gap: 8px;
		color: var(--ink);
		font-weight: 500;
		text-align: left;
	}

	.local-name :global(.icon) {
		color: var(--faint);
	}

	.local-name:hover,
	.list-name:hover {
		color: var(--accent-text);
	}

	.list-name {
		margin-right: 8px;
	}

	.attrs {
		max-width: 280px;
	}

	.attr {
		margin: 0 6px 2px 0;
		padding: 1px 6px;
		border-radius: 4px;
		background: var(--hover);
		color: var(--ink-2);
		font-family: var(--mono);
		font-size: 12px;
	}

	.attr:hover {
		background: var(--accent-soft);
		color: var(--accent-text);
	}

	.end {
		justify-content: flex-end;
	}

	.nowrap {
		flex-wrap: nowrap;
	}

	.local-text {
		min-height: 360px;
	}

	@media (max-width: 720px) {
		.facts {
			grid-template-columns: repeat(2, minmax(0, 1fr));
		}

		.facts > div {
			border-left: 0;
			border-top: 1px solid var(--line);
			padding: 12px 16px;
		}

		.facts > div:first-child {
			border-top: 0;
		}

		.facts .wide {
			grid-column: 1 / -1;
		}

		.locals li {
			grid-template-columns: 1fr auto;
		}

		.locals li > .muted {
			display: none;
		}
	}
</style>
