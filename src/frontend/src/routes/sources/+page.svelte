<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import ListFormatHelp from '#lib/components/ListFormatHelp.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import SourceFormFields from '#lib/components/SourceForm.svelte';
	import { formatDateTime } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { label, refreshStatuses, sourceKinds } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { emptySourceForm, formToRequest, sourceOrigin } from '#lib/sources.js';
	import { attempt } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type Source = components['schemas']['DomainSourceDto'];

	const sources = new Resource(() => unwrap(api.GET('/api/domain-sources')));

	let createOpen = $state(false);
	let form = $state(emptySourceForm());
	let saving = $state(false);
	let refreshing = $state<Record<string, boolean>>({});

	function openCreate() {
		form = emptySourceForm();
		createOpen = true;
	}

	async function create() {
		saving = true;
		const created = await attempt(
			() => unwrap(api.POST('/api/domain-sources', { body: formToRequest(form) })),
			'Источник добавлен, загрузка началась'
		);
		saving = false;
		if (!created) return;
		createOpen = false;
		void sources.reload();
	}

	async function refresh(id: string) {
		refreshing[id] = true;
		await trackJob(api.POST('/api/domain-sources/{id}/refresh', { params: { path: { id } } }));
		refreshing[id] = false;
		void sources.reload();
	}

	async function remove(source: Source) {
		if (!confirm(`Удалить источник «${source.name}»?`)) return;
		const done = await attempt(
			() => unwrap(api.DELETE('/api/domain-sources/{id}', { params: { path: { id: source.id } } })),
			'Источник удалён'
		);
		if (done !== undefined) void sources.reload();
	}
</script>

<div class="toolbar">
	<h1>Источники списков</h1>
	<button class="secondary" onclick={() => sources.reload()}>Обновить</button>
	<button onclick={openCreate}>Добавить</button>
</div>

<Loadable resource={sources}>
	{#snippet children(list)}
		<div class="table-wrap">
			<table>
				<thead>
					<tr>
						<th>Источник</th>
						<th>Откуда</th>
						<th>Списков</th>
						<th>Маршрутов</th>
						<th>Обновлён</th>
						<th></th>
					</tr>
				</thead>
				<tbody>
					{#each list as source (source.id)}
						{@const last = source.lastRefresh}
						{@const status = label(refreshStatuses, last?.status)}
						<tr>
							<td>
								<a href={resolve('/sources/[id]', { id: source.id })}>{source.name}</a>
								<div class="muted small">{sourceKinds[source.kind] ?? source.kind}</div>
							</td>
							<td class="origin">
								{sourceOrigin(source)}
								{#if source.revision}<div class="muted small">
										{source.revision.slice(0, 12)}
									</div>{/if}
							</td>
							<td>
								{#if source.downloaded}{source.listCount}{:else}<Badge tone="warn"
										>не загружен</Badge
									>{/if}
							</td>
							<td>{source.routeCount}</td>
							<td>
								{#if last}
									<Badge tone={status.tone}>{status.label}</Badge>
									<div class="muted small">{formatDateTime(last.at)}</div>
									{#if last.error}<div class="error small">{last.error}</div>{/if}
								{:else}—{/if}
							</td>
							<td class="actions">
								{#if source.kind !== 'Local'}
									<button
										class="small"
										disabled={refreshing[source.id]}
										onclick={() => refresh(source.id)}
									>
										{refreshing[source.id] ? 'Обновляется…' : 'Обновить'}
									</button>
								{/if}
								{#if !source.builtIn}
									<button class="danger small" onclick={() => remove(source)}>Удалить</button>
								{/if}
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/snippet}
</Loadable>

<div class="help"><ListFormatHelp /></div>

<Dialog bind:open={createOpen} title="Новый источник">
	<SourceFormFields bind:form editing={false} />
	{#snippet actions()}
		<button class="secondary" onclick={() => (createOpen = false)}>Отмена</button>
		<button disabled={saving || !form.name.trim()} onclick={create}>Добавить</button>
	{/snippet}
</Dialog>

<style>
	.small {
		font-size: 0.85em;
	}

	.origin {
		overflow-wrap: anywhere;
	}

	.help {
		margin-top: var(--gap);
	}
</style>
