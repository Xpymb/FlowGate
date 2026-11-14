<script lang="ts">
	import { untrack } from 'svelte';
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import { toNumber } from '#lib/format.js';
	import type { components } from '#lib/api/schema.js';

	type Preview = components['schemas']['DomainListPreviewDto'];

	interface Props {
		sourceId: string;
		listName: string;
		attrs?: string;
		editableFilter?: boolean;
	}

	let { sourceId, listName, attrs = $bindable(''), editableFilter = true }: Props = $props();

	let preview = $state<Preview>();
	let error = $state<unknown>();
	let search = $state('');

	const needle = $derived(search.trim().toLowerCase());
	const found = $derived(
		needle
			? (preview?.entries.filter((entry) => entry.toLowerCase().includes(needle)) ?? [])
			: (preview?.entries ?? [])
	);
	const skipped = $derived(
		preview
			? (toNumber(preview.skippedKeyword) ?? 0) +
					(toNumber(preview.skippedRegexp) ?? 0) +
					(toNumber(preview.skippedInvalid) ?? 0)
			: 0
	);

	$effect(() => {
		const id = sourceId;
		const name = listName;
		const filter = editableFilter ? untrack(() => attrs) : attrs;
		void load(id, name, filter);
	});

	async function load(id: string, name: string, filter: string) {
		preview = undefined;
		error = undefined;
		try {
			preview = await unwrap(
				api.GET('/api/domain-sources/{id}/lists/{name}', {
					params: { path: { id, name }, query: { attrs: filter.trim() || undefined } }
				})
			);
		} catch (caught) {
			error = caught;
		}
	}
</script>

<div class="form">
	{#if editableFilter}
		<label>
			Фильтр атрибутов
			<div class="row">
				<input bind:value={attrs} placeholder="например ads или !ads" />
				<button class="secondary" onclick={() => load(sourceId, listName, attrs)}>Применить</button>
			</div>
		</label>
	{:else if attrs}
		<p class="muted">Фильтр атрибутов: <code>@{attrs}</code></p>
	{/if}
	{#if error}
		<p class="error">{errorMessage(error)}</p>
	{:else if !preview}
		<p class="muted">Загрузка…</p>
	{:else}
		<p>
			В Keenetic уйдёт {preview.total}: доменов {preview.domains}, IPv4 {preview.ipv4}, IPv6 {preview.ipv6}.
			{#if skipped > 0}
				<br /><span class="muted">
					Пропущено: keyword {preview.skippedKeyword}, regexp {preview.skippedRegexp}, некорректных {preview.skippedInvalid}.
				</span>
			{/if}
		</p>
		<label>
			Поиск
			<input bind:value={search} placeholder="часть домена или адреса" />
			{#if needle}<span class="hint">найдено {found.length} из {preview.entries.length}</span>{/if}
		</label>
		<pre class="entries">{found.join('\n')}</pre>
		{#if preview.truncated}<p class="muted">Показана часть элементов.</p>{/if}
	{/if}
</div>

<style>
	.row {
		display: flex;
		gap: 8px;
	}

	.entries {
		max-height: 50vh;
		overflow: auto;
		margin: 0;
		padding: 8px;
		background: var(--bg);
		border-radius: var(--radius);
	}
</style>
