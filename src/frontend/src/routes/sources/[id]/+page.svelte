<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import { onChange } from '#lib/events.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import ListFormatHelp from '#lib/components/ListFormatHelp.svelte';
	import ListPreview from '#lib/components/ListPreview.svelte';
	import SourceFormFields from '#lib/components/SourceForm.svelte';
	import UpdateButton from '#lib/components/UpdateButton.svelte';
	import { formatDateTime, toNumber } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { label, refreshStatuses, sourceKinds } from '#lib/labels.js';
	import { formToRequest, sourceOrigin, sourceToForm, type SourceForm } from '#lib/sources.js';
	import { attempt, attemptOk } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type Source = components['schemas']['DomainSourceDto'];
	type ListSummary = components['schemas']['DomainListSummaryDto'];
	type LocalList = components['schemas']['LocalListDto'];
	type UpdateVia = components['schemas']['UpdateVia'];

	const visibleLimit = 300;
	const id = $derived(page.params.id ?? '');

	let source = $state<Source>();
	let loadError = $state<unknown>();
	let lists = $state<ListSummary[]>([]);
	let listsError = $state<unknown>();
	let query = $state('');
	let localLists = $state<LocalList[]>([]);
	let refreshing = $state(false);

	let editOpen = $state(false);
	let editForm = $state<SourceForm>(sourceToFormSafe());
	let saving = $state(false);

	let previewOpen = $state(false);
	let previewName = $state('');
	let previewAttrs = $state('');

	let localOpen = $state(false);
	let localIsNew = $state(false);
	let localName = $state('');
	let localText = $state('');

	let searchTimer: ReturnType<typeof setTimeout> | undefined;

	$effect(() => {
		void loadAll(id);
	});

	$effect(() => onChange(['domain-sources', 'local-lists'], () => void loadAll(id)));

	function sourceToFormSafe(): SourceForm {
		return source
			? sourceToForm(source)
			: sourceToForm({ name: '', kind: 'Local', autoUpdate: false } as Source);
	}

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

	function search() {
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
		editForm = sourceToFormSafe();
		editOpen = true;
	}

	async function saveEdit() {
		saving = true;
		const saved = await attempt(
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
		if (!saved) return;
		source = saved;
		editOpen = false;
	}

	async function remove() {
		if (!source || !confirm(`Удалить источник «${source.name}»?`)) return;
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

	function routeQuery(name: string, attrs: string): string {
		const pairs: [string, string][] = [
			['source', id],
			['list', name]
		];
		if (attrs.trim()) pairs.push(['attrs', attrs.trim()]);
		return pairs.map(([key, value]) => `${key}=${encodeURIComponent(value)}`).join('&');
	}

	function openLocal(list?: LocalList) {
		localIsNew = !list;
		localName = list?.name ?? '';
		localText = list?.text ?? '';
		localOpen = true;
	}

	async function saveLocal() {
		const name = localName.trim();
		const saved = await attempt(
			() =>
				unwrap(
					api.PUT('/api/domain-sources/{id}/local-lists/{name}', {
						params: { path: { id, name } },
						body: { text: localText }
					})
				),
			'Список сохранён'
		);
		if (!saved) return;
		localOpen = false;
		await Promise.all([loadLocal(), loadLists()]);
	}

	async function removeLocal(name: string) {
		if (!confirm(`Удалить список «${name}»?`)) return;
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
</script>

<div class="toolbar">
	<h1>{source?.name ?? 'Источник'}</h1>
	<a href={resolve('/sources')}>← Все источники</a>
</div>

{#if loadError}
	<p class="error">{errorMessage(loadError)}</p>
{:else if !source}
	<p class="muted">Загрузка…</p>
{:else}
	{@const last = source.lastRefresh}
	{@const status = label(refreshStatuses, last?.status)}
	<div class="stack">
		<section class="card">
			<dl class="props">
				<dt>Вид</dt>
				<dd>{sourceKinds[source.kind] ?? source.kind}{source.builtIn ? ' (встроенный)' : ''}</dd>
				<dt>Откуда</dt>
				<dd>{sourceOrigin(source)}</dd>
				{#if source.revision}
					<dt>Версия</dt>
					<dd><code>{source.revision}</code></dd>
				{/if}
				<dt>Списков</dt>
				<dd>{source.listCount}</dd>
				<dt>Маршрутов</dt>
				<dd>{source.routeCount}</dd>
				{#if source.kind !== 'Local'}
					<dt>Автообновление</dt>
					<dd>{source.autoUpdate ? 'да' : 'нет'}</dd>
				{/if}
				{#if last}
					<dt>Обновлён</dt>
					<dd><Badge tone={status.tone}>{status.label}</Badge> {formatDateTime(last.at)}</dd>
				{/if}
			</dl>
			{#if last?.error}<p class="error">{last.error}</p>{/if}
			{#if last && last.errors.length > 0}
				<details>
					<summary>Ошибки в списках: {last.errors.length}</summary>
					<ul class="plain error small">
						{#each last.errors as error (error)}<li>{error}</li>{/each}
					</ul>
				</details>
			{/if}
			<div class="actions buttons">
				{#if source.kind !== 'Local'}
					<UpdateButton label="Обновить" busy={refreshing} onrun={refresh} />
				{/if}
				{#if !source.builtIn}
					<button class="secondary" onclick={openEdit}>Изменить</button>
					<button class="danger" onclick={remove}>Удалить</button>
				{/if}
			</div>
		</section>

		{#if source.kind === 'Local'}
			<section class="card">
				<div class="toolbar">
					<h2>Локальные списки</h2>
					<button class="small" onclick={() => openLocal()}>Новый список</button>
				</div>
				{#if localLists.length === 0}
					<p class="empty">Списков нет</p>
				{:else}
					<ul class="locals">
						{#each localLists as list (list.name)}
							<li>
								<code>{list.name}</code>
								<span class="muted small">{formatDateTime(list.updatedAt)}</span>
								<span class="actions">
									<button class="secondary small" onclick={() => openLocal(list)}>Править</button>
									<button class="danger small" onclick={() => removeLocal(list.name)}
										>Удалить</button
									>
								</span>
							</li>
						{/each}
					</ul>
				{/if}
				<ListFormatHelp />
			</section>
		{/if}

		<section class="card">
			<div class="toolbar">
				<h2>Каталог</h2>
				<input class="search" placeholder="Поиск списка" bind:value={query} oninput={search} />
			</div>
			{#if listsError}
				<p class="error">{errorMessage(listsError)}</p>
			{:else if !source.downloaded}
				<p class="empty">Источник ещё не загружен</p>
			{:else if lists.length === 0}
				<p class="empty">Ничего не найдено</p>
			{:else}
				<div class="table-wrap">
					<table>
						<thead>
							<tr>
								<th>Список</th>
								<th>Доменов</th>
								<th>IPv4</th>
								<th>IPv6</th>
								<th>Пропущено</th>
								<th>Атрибуты</th>
								<th></th>
							</tr>
						</thead>
						<tbody>
							{#each lists.slice(0, visibleLimit) as list (list.name)}
								<tr>
									<td><code>{list.name}</code></td>
									<td>{list.domains}</td>
									<td>{list.ipv4}</td>
									<td>{list.ipv6}</td>
									<td class:muted={(toNumber(list.skipped) ?? 0) === 0}>{list.skipped}</td>
									<td class="attrs">
										{#each list.attributes as attr (attr)}
											<button class="link" onclick={() => openPreview(list.name, attr)}
												>@{attr}</button
											>
										{/each}
									</td>
									<td class="actions">
										<button class="secondary small" onclick={() => openPreview(list.name)}
											>Состав</button
										>
										<a class="small" href={resolve(`/routes?${routeQuery(list.name, '')}`)}
											>В маршрут</a
										>
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
				{#if lists.length > visibleLimit}
					<p class="muted">Показаны первые {visibleLimit} из {lists.length} — уточните поиск.</p>
				{/if}
			{/if}
		</section>
	</div>
{/if}

<Dialog bind:open={previewOpen} title={`Список ${previewName}`} wide>
	<ListPreview sourceId={id} listName={previewName} bind:attrs={previewAttrs} />
	{#snippet actions()}
		<button class="secondary" onclick={() => (previewOpen = false)}>Закрыть</button>
		<a class="button" href={resolve(`/routes?${routeQuery(previewName, previewAttrs)}`)}
			>Направить в соединение</a
		>
	{/snippet}
</Dialog>

<Dialog bind:open={editOpen} title="Источник">
	<SourceFormFields bind:form={editForm} editing={true} />
	{#snippet actions()}
		<button class="secondary" onclick={() => (editOpen = false)}>Отмена</button>
		<button disabled={saving || !editForm.name.trim()} onclick={saveEdit}>Сохранить</button>
	{/snippet}
</Dialog>

<Dialog bind:open={localOpen} title={localIsNew ? 'Новый список' : `Список ${localName}`} wide>
	<div class="form">
		{#if localIsNew}
			<label>
				Имя
				<input bind:value={localName} placeholder="telegram" maxlength="64" />
				<span class="hint">Латиница в нижнем регистре, цифры, - . _ !</span>
			</label>
		{/if}
		<label>
			Строки списка
			<textarea bind:value={localText} rows="16" spellcheck="false"></textarea>
		</label>
	</div>
	{#snippet actions()}
		<button class="secondary" onclick={() => (localOpen = false)}>Отмена</button>
		<button disabled={!localName.trim()} onclick={saveLocal}>Сохранить</button>
	{/snippet}
</Dialog>

<style>
	.small {
		font-size: 0.85em;
	}

	.buttons {
		margin-top: 12px;
	}

	.toolbar h2 {
		margin: 0 auto 0 0;
	}

	.search {
		width: 240px;
	}

	.attrs {
		max-width: 260px;
	}

	button.link {
		padding: 0 4px 0 0;
		border: none;
		background: none;
		color: var(--accent);
		font-size: 0.88em;
	}

	.locals {
		list-style: none;
		margin: 0 0 12px;
		padding: 0;
	}

	.locals li {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 6px 0;
		border-bottom: 1px solid var(--border);
	}

	.locals .actions {
		margin-left: auto;
	}
</style>
