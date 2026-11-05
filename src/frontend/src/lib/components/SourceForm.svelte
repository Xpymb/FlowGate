<script lang="ts">
	import type { SourceForm } from '#lib/sources.js';

	interface Props {
		form: SourceForm;
		editing: boolean;
	}

	let { form = $bindable(), editing }: Props = $props();
	const isDat = $derived(form.kind === 'GeositeDat' || form.kind === 'GeoipDat');
</script>

<div class="form">
	<label>
		Имя
		<input bind:value={form.name} maxlength="64" />
	</label>
	<label>
		Вид
		<select bind:value={form.kind} disabled={editing}>
			<option value="GitHubRepository">GitHub-репозиторий в формате v2fly</option>
			<option value="GeositeDat">geosite.dat</option>
			<option value="GeoipDat">geoip.dat</option>
			<option value="Local">Локальные списки</option>
		</select>
	</label>

	{#if form.kind === 'GitHubRepository'}
		<label>
			Репозиторий
			<input
				bind:value={form.repository}
				placeholder="owner/repo или https://github.com/owner/repo"
			/>
		</label>
		<label>
			Ветка
			<input bind:value={form.branch} placeholder="по умолчанию" />
		</label>
	{:else if isDat}
		<div class="mode">
			<label class="check">
				<input type="radio" bind:group={form.datMode} value="release" /> Из релиза GitHub
			</label>
			<label class="check">
				<input type="radio" bind:group={form.datMode} value="url" /> По прямой ссылке
			</label>
		</div>
		{#if form.datMode === 'release'}
			<label>
				Репозиторий релизов
				<input bind:value={form.releaseRepository} placeholder="owner/repo" />
			</label>
			<label>
				Файл в релизе
				<input
					bind:value={form.asset}
					placeholder={form.kind === 'GeoipDat' ? 'geoip.dat' : 'geosite.dat'}
				/>
			</label>
			<label>
				Файл контрольной суммы
				<input
					bind:value={form.checksumAsset}
					placeholder="необязательно, например geoip.dat.sha256sum"
				/>
			</label>
		{:else}
			<label>
				Ссылка на файл
				<input bind:value={form.url} type="url" placeholder="https://…" />
			</label>
			<label>
				Ссылка на SHA-256
				<input bind:value={form.checksumUrl} type="url" placeholder="необязательно" />
			</label>
		{/if}
	{:else}
		<p class="hint">Списки создаются и правятся на странице источника.</p>
	{/if}

	{#if form.kind !== 'Local'}
		<label class="check">
			<input type="checkbox" bind:checked={form.autoUpdate} /> Обновлять по расписанию
		</label>
	{/if}
</div>

<style>
	.mode {
		display: flex;
		gap: 16px;
	}
</style>
