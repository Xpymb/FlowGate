<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { formatDateTime } from '#lib/format.js';
	import { hotkey } from '#lib/hotkeys.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { label, refreshStatuses, sourceKinds } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { emptySourceForm, formToRequest, sourceOrigin } from '#lib/sources.js';
	import { matchesQuery, UrlParam } from '#lib/table.svelte.js';
	import { attempt, attemptOk } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import ListFormatHelp from '#lib/components/ListFormatHelp.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import Menu from '#lib/components/Menu.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SearchInput from '#lib/components/SearchInput.svelte';
	import SourceFormFields from '#lib/components/SourceForm.svelte';
	import UpdateButton from '#lib/components/UpdateButton.svelte';

	type Source = components['schemas']['DomainSourceDto'];
	type UpdateVia = components['schemas']['UpdateVia'];

	const sources = new Resource(() => unwrap(api.GET('/api/domain-sources')), {
		live: ['domain-sources', 'dns-routes']
	});
	const query = new UrlParam('q');

	let createOpen = $state(false);
	let form = $state(emptySourceForm());
	let saving = $state(false);
	let refreshing = $state<Record<string, boolean>>({});

	const visible = $derived(
		(sources.data ?? []).filter((source) =>
			matchesQuery(query.value, [source.name, sourceOrigin(source), sourceKinds[source.kind]])
		)
	);

	$effect(() => hotkey('n', openCreate));

	function openCreate() {
		form = emptySourceForm();
		createOpen = true;
	}

	async function create() {
		if (!form.name.trim()) return;
		saving = true;
		const created = await attempt(
			() => unwrap(api.POST('/api/domain-sources', { body: formToRequest(form) })),
			'Источник добавлен, загрузка началась'
		);
		saving = false;
		if (!created) return;
		createOpen = false;
		await goto(resolve('/sources/[id]', { id: created.id }));
	}

	async function refresh(id: string, via: UpdateVia) {
		refreshing[id] = true;
		await trackJob(
			api.POST('/api/domain-sources/{id}/refresh', { params: { path: { id }, query: { via } } })
		);
		refreshing[id] = false;
		void sources.reload();
	}

	async function remove(source: Source) {
		const ok = await ask({
			title: `Удалить источник «${source.name}»?`,
			message:
				Number(source.routeCount) > 0
					? `На его списки ссылаются маршруты (${source.routeCount}) — они перестанут обновляться.`
					: 'Загруженные списки удалятся вместе с ним.',
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		const done = await attemptOk(
			() => unwrap(api.DELETE('/api/domain-sources/{id}', { params: { path: { id: source.id } } })),
			'Источник удалён'
		);
		if (done) void sources.reload();
	}
</script>

<PageHeader
	title="Источники списков"
	description="Откуда берутся списки доменов и адресов для маршрутов: репозитории GitHub, файлы geosite/geoip или ваши собственные."
>
	{#snippet actions()}
		<button class="btn primary" onclick={openCreate} title="Клавиша N">
			<Icon name="plus" size={16} />Добавить источник
		</button>
	{/snippet}
</PageHeader>

<Loadable resource={sources}>
	{#snippet children(list)}
		{#if list.length > 5}
			<div class="toolbar">
				<SearchInput
					value={query.value}
					placeholder="Имя или адрес"
					oninput={(value) => query.set(value)}
				/>
			</div>
		{/if}
		<div class="panel">
			<div class="table-scroll">
				<table class="table">
					<thead>
						<tr>
							<th>Источник</th>
							<th>Откуда</th>
							<th class="num">Списков</th>
							<th class="num">Маршрутов</th>
							<th>Обновление</th>
							<th class="actions-cell"><span class="visually-hidden">Действия</span></th>
						</tr>
					</thead>
					<tbody>
						{#each visible as source (source.id)}
							{@const last = source.lastRefresh}
							{@const status = label(refreshStatuses, last?.status)}
							<tr>
								<td>
									<a class="name" href={resolve('/sources/[id]', { id: source.id })}
										>{source.name}</a
									>
									<div class="sub">
										{sourceKinds[source.kind] ?? source.kind}{source.builtIn ? ' · встроенный' : ''}
									</div>
								</td>
								<td class="origin">
									<span class="mono small">{sourceOrigin(source)}</span>
									{#if source.revision}<div class="sub mono">
											{source.revision.slice(0, 12)}
										</div>{/if}
								</td>
								<td class="num">
									{#if source.downloaded}{source.listCount}{:else}<Badge tone="warn"
											>не загружен</Badge
										>{/if}
								</td>
								<td class="num">
									{#if Number(source.routeCount) > 0}
										<a href={resolve(`/routes?q=${encodeURIComponent(source.name)}`)}
											>{source.routeCount}</a
										>
									{:else}<span class="faint">0</span>{/if}
								</td>
								<td>
									{#if last}
										<Badge tone={status.tone} dot>{status.label}</Badge>
										<div class="sub">{formatDateTime(last.at)}</div>
										{#if last.error}<div class="error-text small">{last.error}</div>{/if}
									{:else if source.kind === 'Local'}
										<span class="faint">правится вручную</span>
									{:else}<span class="faint">ещё не было</span>{/if}
								</td>
								<td class="actions-cell">
									<div class="row nowrap">
										{#if source.kind !== 'Local'}
											<UpdateButton
												small
												label="Обновить"
												busy={refreshing[source.id]}
												onrun={(via) => refresh(source.id, via)}
											/>
										{/if}
										<Menu
											ariaLabel="Действия с {source.name}"
											items={[
												{
													label:
														source.kind === 'Local'
															? 'Открыть и править списки'
															: 'Открыть каталог',
													icon: 'list',
													href: resolve('/sources/[id]', { id: source.id })
												},
												'separator',
												{
													label: source.builtIn ? 'Встроенный, не удаляется' : 'Удалить',
													icon: 'trash',
													danger: true,
													disabled: source.builtIn,
													onclick: () => remove(source)
												}
											]}
										/>
									</div>
								</td>
							</tr>
						{:else}
							<tr>
								<td colspan="6">
									<EmptyState bare icon="search" title="Ничего не нашлось">
										<button class="btn" onclick={() => query.set('')}>Сбросить поиск</button>
									</EmptyState>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		</div>
	{/snippet}
</Loadable>

<div class="help"><ListFormatHelp /></div>

<Dialog
	bind:open={createOpen}
	variant="side"
	title="Новый источник"
	description="После сохранения FlowGate сразу загрузит списки."
	onsubmit={create}
>
	<SourceFormFields bind:form editing={false} />
	{#snippet actions()}
		<button class="btn" onclick={() => (createOpen = false)}>Отмена</button>
		<button class="btn primary" disabled={!form.name.trim()} aria-busy={saving} onclick={create}>
			Добавить источник
		</button>
	{/snippet}
</Dialog>

<style>
	.toolbar {
		margin-bottom: 16px;
	}

	.name {
		color: var(--ink);
		font-weight: 500;
	}

	.origin {
		max-width: 320px;
		overflow-wrap: anywhere;
	}

	.nowrap {
		flex-wrap: nowrap;
		justify-content: flex-end;
	}

	.help {
		margin-top: 24px;
	}
</style>
