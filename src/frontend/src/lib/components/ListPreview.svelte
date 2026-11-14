<script lang="ts">
	import { untrack } from 'svelte';
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import { toNumber } from '#lib/format.js';
	import type { components } from '#lib/api/schema.js';
	import Field from './Field.svelte';
	import SearchInput from './SearchInput.svelte';

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
	let filterTimer: ReturnType<typeof setTimeout> | undefined;

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

	function onFilter(value: string) {
		attrs = value;
		clearTimeout(filterTimer);
		filterTimer = setTimeout(() => void load(sourceId, listName, value), 400);
	}
</script>

<div class="preview">
	{#if editableFilter}
		<Field label="Фильтр атрибутов" hint="Например ads — только строки с @ads, !ads — без него">
			<input
				value={attrs}
				placeholder="без фильтра"
				oninput={(event) => onFilter(event.currentTarget.value)}
			/>
		</Field>
	{:else if attrs}
		<p class="muted">Фильтр атрибутов: <code>@{attrs}</code></p>
	{/if}

	{#if error}
		<p class="error-text">{errorMessage(error)}</p>
	{:else if !preview}
		<div class="stats loading" aria-busy="true">Считаем состав списка…</div>
	{:else}
		<div class="stats">
			<div>
				<span class="value num">{preview.total}</span><span class="muted">всего в Keenetic</span>
			</div>
			<div><span class="value num">{preview.domains}</span><span class="muted">доменов</span></div>
			<div><span class="value num">{preview.ipv4}</span><span class="muted">IPv4</span></div>
			<div><span class="value num">{preview.ipv6}</span><span class="muted">IPv6</span></div>
		</div>
		{#if skipped > 0}
			<p class="muted small">
				Пропущено: keyword {preview.skippedKeyword}, regexp {preview.skippedRegexp}, некорректных
				{preview.skippedInvalid} — Keenetic их не поддерживает.
			</p>
		{/if}
		<div class="row">
			<SearchInput bind:value={search} placeholder="Часть домена или адреса" shortcut={false} />
			{#if needle}<span class="muted small num"
					>найдено {found.length} из {preview.entries.length}</span
				>{/if}
		</div>
		<pre class="entries">{found.join('\n') || 'Ничего не найдено'}</pre>
		{#if preview.truncated}<p class="muted small">Показана часть элементов.</p>{/if}
	{/if}
</div>

<style>
	.preview {
		display: grid;
		gap: 16px;
	}

	.stats {
		display: grid;
		grid-template-columns: repeat(4, minmax(0, 1fr));
		border: 1px solid var(--line);
		border-radius: var(--r-md);
	}

	.stats.loading {
		display: block;
		padding: 18px;
		color: var(--muted);
	}

	.stats div {
		display: grid;
		gap: 2px;
		padding: 12px 16px;
		border-left: 1px solid var(--line);
	}

	.stats div:first-child {
		border-left: 0;
	}

	.value {
		font-size: 20px;
		font-weight: 600;
	}

	.stats .muted {
		font-size: 12.5px;
	}

	.entries {
		max-height: 46vh;
		overflow: auto;
		margin: 0;
		padding: 12px 14px;
		border-radius: var(--r-sm);
		background: var(--surface-2);
		border: 1px solid var(--line);
		line-height: 1.6;
	}

	@media (max-width: 560px) {
		.stats {
			grid-template-columns: repeat(2, minmax(0, 1fr));
		}

		.stats div:nth-child(3) {
			border-left: 0;
		}

		.stats div:nth-child(n + 3) {
			border-top: 1px solid var(--line);
		}
	}
</style>
