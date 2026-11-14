<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { ApiError, errorMessage, unwrap } from '#lib/api/errors.js';
	import { toNumber } from '#lib/format.js';
	import { waitForJob } from '#lib/jobs.js';
	import { geositeSourceId, listKey, presets, suggestions } from '#lib/setup/presets.js';
	import { setup } from '#lib/setup/setup.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Icon from '../Icon.svelte';
	import SearchInput from '../SearchInput.svelte';

	type ListSummary = components['schemas']['DomainListSummaryDto'];

	const draft = $derived(setup.draft);
	const several = $derived(draft.connections.length > 1);

	/** Каталог domain-list-community: проверка, загрузка напрямую с GitHub, готов, не загрузился. */
	let catalog = $state<'checking' | 'downloading' | 'ready' | 'failed'>('checking');
	let catalogError = $state('');
	let query = $state('');
	let results = $state<ListSummary[]>([]);
	let searching = $state(false);
	/** Список, состав которого раскрыт, и загруженные составы. */
	let opened = $state('');
	let compositions = $state<Record<string, { total: number; entries: string[] } | 'error'>>({});
	let timer: ReturnType<typeof setTimeout> | undefined;

	const freeSuggestions = $derived(
		suggestions.filter((name) => !setup.isSelected({ sourceId: geositeSourceId, name }))
	);

	$effect(() => {
		void prepareCatalog();
		return () => clearTimeout(timer);
	});

	/** Каталог качается сам: это данные списков, конфигурация роутера не меняется. */
	async function prepareCatalog() {
		try {
			const source = await unwrap(
				api.GET('/api/domain-sources/{id}', { params: { path: { id: geositeSourceId } } })
			);
			if (source.downloaded) {
				catalog = 'ready';
				return;
			}
			catalog = 'downloading';
			const job = await unwrap(
				api.POST('/api/domain-sources/{id}/refresh', {
					params: { path: { id: geositeSourceId }, query: { via: 'Direct' } }
				})
			);
			await waitForJob(job);
			catalog = 'ready';
			if (query.trim()) void search();
		} catch (error) {
			catalog = 'failed';
			catalogError =
				error instanceof ApiError && error.code !== 'JOB_FAILED'
					? errorMessage(error)
					: 'GitHub недоступен напрямую. Добавляйте списки подсказками ниже: файл скачается через сервер при запуске.';
		}
	}

	function onsearch(value: string) {
		query = value;
		clearTimeout(timer);
		timer = setTimeout(() => void search(), 250);
	}

	async function search() {
		const needle = query.trim().toLowerCase();
		if (!needle || catalog !== 'ready') {
			results = [];
			return;
		}
		searching = true;
		try {
			const found = await unwrap(
				api.GET('/api/domain-sources/{id}/lists', {
					params: { path: { id: geositeSourceId }, query: { query: needle } }
				})
			);
			if (query.trim().toLowerCase() !== needle) return;
			const exact = found.filter((list) => list.name === needle);
			const rest = found.filter((list) => list.name !== needle);
			results = [...exact, ...rest].slice(0, 40);
		} catch {
			results = [];
		} finally {
			searching = false;
		}
	}

	function toggle(name: string) {
		const list = { sourceId: geositeSourceId, name };
		if (setup.isSelected(list)) setup.removeList(list);
		else setup.addList(list);
	}

	/** Раскрывает состав: число элементов и первые домены. */
	async function showComposition(name: string) {
		opened = opened === name ? '' : name;
		if (!opened || compositions[name]) return;
		try {
			const preview = await unwrap(
				api.GET('/api/domain-sources/{id}/lists/{name}', {
					params: { path: { id: geositeSourceId, name } }
				})
			);
			compositions[name] = {
				total: toNumber(preview.total) ?? preview.entries.length,
				entries: preview.entries.slice(0, 12)
			};
		} catch {
			compositions[name] = 'error';
		}
	}

	function size(list: ListSummary): string {
		const domains = toNumber(list.domains) ?? 0;
		const ips = (toNumber(list.ipv4) ?? 0) + (toNumber(list.ipv6) ?? 0);
		return ips > 0 ? `доменов ${domains}, адресов ${ips}` : `доменов ${domains}`;
	}
</script>

<div class="form">
	<section class="block" aria-labelledby="popular-title">
		<h4 id="popular-title">Популярные</h4>
		<div class="tiles">
			{#each presets as preset (preset.id)}
				{@const on = setup.presetOn(preset)}
				<label class="tile" class:on>
					<input type="checkbox" checked={on} onchange={() => setup.togglePreset(preset)} />
					<span class="tile-title">{preset.title}</span>
					<span class="tile-list">{preset.lists.map((list) => list.name).join(', ')}</span>
				</label>
			{/each}
		</div>
	</section>

	<section class="block" aria-labelledby="catalog-title">
		<div class="block-head">
			<h4 id="catalog-title">Списки v2fly</h4>
			<span class="muted small" role="status">
				{#if catalog === 'checking'}
					Проверяю каталог…
				{:else if catalog === 'downloading'}
					<span class="spinner" aria-hidden="true"></span>Загружаю каталог с GitHub…
				{/if}
			</span>
		</div>
		<p class="muted small">
			Готовые списки доменов сообщества v2fly: по одному на сервис или категорию. Найдите сервис по
			названию латиницей.
		</p>

		{#if catalog === 'ready'}
			<div class="search-wide">
				<SearchInput
					value={query}
					placeholder="Например, youtube, steam или category-ai"
					label="Поиск списка"
					shortcut={false}
					oninput={onsearch}
				/>
			</div>
		{:else if catalog === 'failed'}
			<p class="error-text small">{catalogError}</p>
		{/if}

		{#if query.trim() && catalog === 'ready'}
			{#if !searching && results.length === 0}
				<p class="muted small">Списка «{query.trim()}» нет. Попробуйте другое написание.</p>
			{:else}
				<ul class="results">
					{#each results as list (list.name)}
						{@const added = setup.isSelected({ sourceId: geositeSourceId, name: list.name })}
						<li>
							<div class="result">
								<span class="result-name">
									<span>{list.name}</span>
									<span class="muted small">{size(list)}</span>
								</span>
								<button
									class="btn ghost sm"
									aria-expanded={opened === list.name}
									onclick={() => showComposition(list.name)}
								>
									Состав
								</button>
								<button
									class="btn sm"
									class:primary={!added}
									aria-pressed={added}
									onclick={() => toggle(list.name)}
								>
									{#if added}<Icon name="check" size={14} />Добавлен{:else}<Icon
											name="plus"
											size={14}
										/>Добавить{/if}
								</button>
							</div>
							{#if opened === list.name}
								{@const composition = compositions[list.name]}
								<p class="composition small">
									{#if composition === 'error'}
										<span class="error-text">Состав не загрузился.</span>
									{:else if composition}
										{`${composition.entries.join(', ')}${composition.total > composition.entries.length ? ` и ещё ${composition.total - composition.entries.length}` : ''}`}
									{:else}
										<span class="muted">Загружаю состав…</span>
									{/if}
								</p>
							{/if}
						</li>
					{/each}
				</ul>
			{/if}
		{:else if freeSuggestions.length > 0}
			<div class="chips" role="group" aria-label="Часто добавляют">
				<span class="muted small">Часто добавляют:</span>
				{#each freeSuggestions as name (name)}
					<button class="chip" onclick={() => toggle(name)}>
						<Icon name="plus" size={13} />{name}
					</button>
				{/each}
			</div>
		{/if}
	</section>

	<section class="block" aria-labelledby="selected-title">
		<div class="block-head">
			<h4 id="selected-title">Выбрано: {draft.selected.length}</h4>
			{#if several}<span class="muted small">Через какое соединение</span>{/if}
		</div>
		{#if draft.selected.length === 0}
			<p class="muted small">Отметьте популярный сервис или добавьте список из каталога.</p>
		{:else}
			<ul class="selected">
				{#each draft.selected as item (listKey(item))}
					<li>
						<span class="selected-name">
							<span>{item.title}</span>
							{#if item.title !== item.name}<span class="muted small">{item.name}</span>{/if}
						</span>
						{#if several}
							<label class="via">
								<span class="visually-hidden">Соединение для {item.title}</span>
								<select bind:value={item.connectionUid}>
									{#each draft.connections as connection, index (connection.uid)}
										<option value={connection.uid}>
											{index + 1}. {setup.connectionLabel(connection)}
										</option>
									{/each}
								</select>
							</label>
						{/if}
						<button
							class="btn ghost icon sm"
							aria-label="Убрать {item.title}"
							onclick={() => setup.removeList(item)}
						>
							<Icon name="close" size={15} />
						</button>
					</li>
				{/each}
			</ul>
		{/if}
		<p class="muted small">
			Каждый список становится DNS-маршрутом: Keenetic отправляет его домены через выбранное
			соединение, остальной трафик идёт напрямую.
		</p>
	</section>
</div>

<style>
	.block {
		display: grid;
		gap: 10px;
	}

	.block + .block {
		padding-top: 18px;
		border-top: 1px solid var(--line);
	}

	.block h4 {
		margin: 0;
		font-size: 14px;
	}

	.block-head {
		display: flex;
		align-items: baseline;
		justify-content: space-between;
		gap: 12px;
	}

	.tiles {
		display: grid;
		grid-template-columns: repeat(4, minmax(0, 1fr));
		gap: 8px;
	}

	.tile {
		position: relative;
		display: grid;
		gap: 2px;
		padding: 10px 12px 10px 36px;
		border: 1px solid var(--line-strong);
		border-radius: var(--r-sm);
		cursor: pointer;
	}

	.tile:hover {
		border-color: var(--faint);
	}

	.tile:has(input:focus-visible) {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	.tile input {
		position: absolute;
		top: 12px;
		left: 12px;
		margin: 0;
	}

	.tile.on {
		border-color: var(--accent);
		background: var(--accent-soft);
	}

	.tile-title {
		font-weight: 500;
	}

	.tile-list {
		overflow: hidden;
		color: var(--muted);
		font-size: 12px;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.spinner {
		display: inline-block;
		width: 10px;
		height: 10px;
		margin-right: 6px;
		border: 2px solid var(--accent);
		border-right-color: transparent;
		border-radius: 999px;
		vertical-align: -1px;
		animation: spin 0.8s linear infinite;
	}

	.chips {
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		gap: 6px;
	}

	.chip {
		display: inline-flex;
		align-items: center;
		gap: 4px;
		height: 26px;
		padding: 0 10px 0 8px;
		border: 1px solid var(--line-strong);
		border-radius: 999px;
		color: var(--ink-2);
		font-size: 13px;
	}

	.chip:hover {
		border-color: var(--accent);
		color: var(--accent-text);
	}

	.chip:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	.results,
	.selected {
		display: grid;
		margin: 0;
		padding: 0;
		list-style: none;
	}

	.results {
		max-height: 300px;
		overflow: auto;
		border: 1px solid var(--line);
		border-radius: var(--r-sm);
	}

	.results li + li,
	.selected li + li {
		border-top: 1px solid var(--line);
	}

	.result {
		display: flex;
		align-items: center;
		gap: 6px;
		padding: 6px 8px 6px 12px;
	}

	.result-name,
	.selected-name {
		display: grid;
		flex: 1;
		min-width: 0;
		overflow-wrap: anywhere;
	}

	.composition {
		margin: 0;
		padding: 0 12px 10px;
		color: var(--ink-2);
		overflow-wrap: anywhere;
	}

	.search-wide :global(.search) {
		width: 100%;
	}

	.selected li {
		display: flex;
		align-items: center;
		gap: 10px;
		padding: 6px 0;
	}

	.via select {
		max-width: 240px;
	}

	@keyframes spin {
		to {
			transform: rotate(360deg);
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.spinner {
			animation-duration: 2.4s;
		}
	}

	@media (max-width: 720px) {
		.tiles {
			grid-template-columns: repeat(2, minmax(0, 1fr));
		}

		.selected li {
			flex-wrap: wrap;
		}

		.via {
			order: 3;
			flex-basis: 100%;
		}

		.via select {
			width: 100%;
			max-width: none;
		}
	}
</style>
