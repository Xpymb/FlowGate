<script lang="ts">
	import type { SourceForm } from '#lib/sources.js';
	import Field from './Field.svelte';

	interface Props {
		form: SourceForm;
		editing: boolean;
	}

	let { form = $bindable(), editing }: Props = $props();
	const isDat = $derived(form.kind === 'GeositeDat' || form.kind === 'GeoipDat');

	const kinds = [
		{ value: 'GitHubRepository', title: 'GitHub-репозиторий', text: 'Списки в формате v2fly' },
		{ value: 'GeositeDat', title: 'geosite.dat', text: 'Файл доменов Xray' },
		{ value: 'GeoipDat', title: 'geoip.dat', text: 'Файл адресов Xray' },
		{ value: 'Local', title: 'Локальные списки', text: 'Редактируются в FlowGate' }
	];
</script>

<div class="form">
	<Field label="Имя">
		<input bind:value={form.name} maxlength="64" placeholder="v2fly community" />
	</Field>

	<Field
		label="Тип"
		group
		hint={editing ? 'Тип источника нельзя изменить после создания.' : undefined}
	>
		<div class="choices two">
			{#each kinds as kind (kind.value)}
				<label class="choice" class:selected={form.kind === kind.value} class:disabled={editing}>
					<input type="radio" bind:group={form.kind} value={kind.value} disabled={editing} />
					<span class="choice-title">{kind.title}</span>
					<span class="choice-text">{kind.text}</span>
				</label>
			{/each}
		</div>
	</Field>

	{#if form.kind === 'GitHubRepository'}
		<Field label="Репозиторий">
			<input
				bind:value={form.repository}
				placeholder="owner/repo или https://github.com/owner/repo"
			/>
		</Field>
		<Field label="Ветка" hint="По умолчанию — основная ветка репозитория">
			<input bind:value={form.branch} placeholder="main" />
		</Field>
	{:else if isDat}
		<Field label="Расположение файла" group>
			<div class="segmented">
				<label class:selected={form.datMode === 'release'}>
					<input type="radio" bind:group={form.datMode} value="release" /> Релиз GitHub
				</label>
				<label class:selected={form.datMode === 'url'}>
					<input type="radio" bind:group={form.datMode} value="url" /> Прямая ссылка
				</label>
			</div>
		</Field>
		{#if form.datMode === 'release'}
			<Field label="Репозиторий релизов">
				<input bind:value={form.releaseRepository} placeholder="owner/repo" />
			</Field>
			<Field label="Файл в релизе">
				<input
					bind:value={form.asset}
					placeholder={form.kind === 'GeoipDat' ? 'geoip.dat' : 'geosite.dat'}
				/>
			</Field>
			<Field label="Файл контрольной суммы" hint="Необязательно">
				<input bind:value={form.checksumAsset} placeholder="geoip.dat.sha256sum" />
			</Field>
		{:else}
			<Field label="Ссылка на файл">
				<input bind:value={form.url} type="url" placeholder="https://…" />
			</Field>
			<Field label="Ссылка на SHA-256" hint="Необязательно">
				<input bind:value={form.checksumUrl} type="url" placeholder="https://…" />
			</Field>
		{/if}
	{:else}
		<p class="muted small">Списки редактируются на странице источника после его создания.</p>
	{/if}

	{#if form.kind !== 'Local'}
		<label class="check">
			<input type="checkbox" bind:checked={form.autoUpdate} />
			<span>Обновлять по расписанию</span>
		</label>
	{/if}
</div>
