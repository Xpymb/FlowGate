<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import { formatBytes, formatDateTime } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { label, xrayStates } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { attempt } from '#lib/toast.svelte.js';

	const status = new Resource(() => unwrap(api.GET('/api/xray')));
	let includePrerelease = $state(false);
	const releases = new Resource(() =>
		unwrap(api.GET('/api/xray/releases', { params: { query: { includePrerelease } } }))
	);
	let logTail = $state(200);
	const logs = new Resource(() =>
		unwrap(api.GET('/api/xray/logs', { params: { query: { tail: logTail } } }))
	);

	let busy = $state(false);

	async function control(action: 'start' | 'stop' | 'restart') {
		busy = true;
		const titles = { start: 'Xray запущен', stop: 'Xray остановлен', restart: 'Xray перезапущен' };
		const result = await attempt(() => unwrap(api.POST(`/api/xray/${action}`)), titles[action]);
		if (result) status.data = result;
		busy = false;
		void logs.reload();
	}

	async function install(version: string | null) {
		busy = true;
		const ok = await trackJob(api.POST('/api/xray/install', { body: { version } }));
		busy = false;
		if (ok) {
			await Promise.all([status.reload(), releases.reload(), logs.reload()]);
		}
	}

	async function refreshReleases() {
		await attempt(async () => {
			releases.data = await unwrap(
				api.GET('/api/xray/releases', { params: { query: { includePrerelease, refresh: true } } })
			);
		});
	}
</script>

<div class="toolbar">
	<h1>Xray</h1>
	<button class="secondary" onclick={() => status.reload()}>Обновить</button>
</div>

<div class="stack">
	<section class="card">
		<Loadable resource={status}>
			{#snippet children(xray)}
				{@const state = label(xrayStates, xray.state)}
				<dl class="props">
					<dt>Состояние</dt>
					<dd><Badge tone={state.tone}>{state.label}</Badge></dd>
					<dt>Версия</dt>
					<dd>
						{xray.version ?? '—'}
						{#if xray.updateAvailable}
							<Badge tone="warn">доступна {xray.latestVersion}</Badge>
						{/if}
					</dd>
					<dt>Автозапуск</dt>
					<dd>{xray.autostart ? 'да' : 'нет'}</dd>
					<dt>Запущен</dt>
					<dd>
						{formatDateTime(xray.startedAt)}{xray.processId ? ` (pid ${xray.processId})` : ''}
					</dd>
					<dt>Перезапусков</dt>
					<dd>{xray.restarts}</dd>
					{#if xray.lastError}
						<dt>Ошибка</dt>
						<dd class="error">{xray.lastError}</dd>
					{/if}
				</dl>
				<div class="actions controls">
					{#if xray.installed}
						{#if xray.state === 'Running' || xray.state === 'Starting'}
							<button disabled={busy} onclick={() => control('restart')}>Перезапустить</button>
							<button class="secondary" disabled={busy} onclick={() => control('stop')}
								>Остановить</button
							>
						{:else}
							<button disabled={busy} onclick={() => control('start')}>Запустить</button>
						{/if}
						{#if xray.updateAvailable}
							<button class="secondary" disabled={busy} onclick={() => install(null)}>
								Обновить до {xray.latestVersion}
							</button>
						{/if}
					{:else}
						<button disabled={busy} onclick={() => install(null)}
							>Установить последнюю версию</button
						>
					{/if}
				</div>
			{/snippet}
		</Loadable>
	</section>

	<section class="card">
		<div class="toolbar">
			<h2>Версии</h2>
			<label class="check">
				<input
					type="checkbox"
					bind:checked={includePrerelease}
					onchange={() => releases.reload()}
				/>
				предварительные
			</label>
			<button class="secondary small" onclick={refreshReleases}>Проверить GitHub</button>
		</div>
		<Loadable resource={releases}>
			{#snippet children(list)}
				{#if list.length === 0}
					<p class="empty">Релизов нет</p>
				{:else}
					<div class="table-wrap">
						<table>
							<thead>
								<tr><th>Версия</th><th>Дата</th><th>Размер</th><th></th></tr>
							</thead>
							<tbody>
								{#each list as release (release.tag)}
									<tr>
										<td>
											{release.version}
											{#if release.isLatest}<Badge tone="accent">последняя</Badge>{/if}
											{#if release.isPrerelease}<Badge tone="warn">pre</Badge>{/if}
										</td>
										<td>{formatDateTime(release.publishedAt)}</td>
										<td>{formatBytes(release.sizeBytes)}</td>
										<td>
											{#if release.isInstalled}
												<Badge tone="ok">установлена</Badge>
											{:else}
												<button
													class="secondary small"
													disabled={busy}
													onclick={() => install(release.version)}>Установить</button
												>
											{/if}
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
				{/if}
			{/snippet}
		</Loadable>
	</section>

	<section class="card">
		<div class="toolbar">
			<h2>Журнал</h2>
			<select bind:value={logTail} onchange={() => logs.reload()} class="tail">
				<option value={100}>100 строк</option>
				<option value={200}>200 строк</option>
				<option value={500}>500 строк</option>
			</select>
			<button class="secondary small" onclick={() => logs.reload()}>Обновить</button>
		</div>
		<Loadable resource={logs}>
			{#snippet children(log)}
				{#if log.lines.length === 0}
					<p class="empty">Журнал пуст</p>
				{:else}
					<pre class="log">{log.lines.map((line) => line.text).join('\n')}</pre>
				{/if}
			{/snippet}
		</Loadable>
	</section>
</div>

<style>
	.controls {
		margin-top: var(--gap);
	}

	.toolbar h2 {
		margin: 0 auto 0 0;
	}

	.check {
		display: flex;
		align-items: center;
		gap: 6px;
		color: var(--muted);
	}

	.tail {
		width: auto;
	}

	.log {
		margin: 0;
		max-height: 420px;
		overflow: auto;
		padding: 8px;
		background: var(--bg);
		border-radius: var(--radius);
		white-space: pre-wrap;
		word-break: break-all;
	}
</style>
