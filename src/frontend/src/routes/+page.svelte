<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import { formatDateTime, formatDuration, formatKilobytes } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { notify } from '#lib/toast.svelte.js';

	const system = unwrap(api.GET('/api/system'));
	const xray = unwrap(api.GET('/api/xray'));
	const keenetic = unwrap(api.GET('/api/keenetic/system'));

	let upgrading = $state(false);

	async function upgrade(version: string | null) {
		if (!confirm(`Обновить FlowGate до ${version ?? 'новой версии'}? Служба перезапустится.`))
			return;
		upgrading = true;
		const ok = await trackJob(api.POST('/api/system/update'));
		if (ok) notify('info', 'Обновление идёт через opkg. Обновите страницу через минуту.');
		else upgrading = false;
	}
</script>

<h1>Обзор</h1>

<div class="grid">
	<section class="card">
		<h2>FlowGate</h2>
		{#await system}
			<p class="muted">Загрузка…</p>
		{:then status}
			<dl class="props">
				<dt>Версия</dt>
				<dd>
					{status.version}
					{#if status.updateAvailable}<span class="warn">→ {status.latestVersion}</span>{/if}
				</dd>
				<dt>Работает</dt>
				<dd>{formatDuration(status.uptimeSeconds)}</dd>
				<dt>Память</dt>
				<dd>{formatKilobytes(status.memoryRssKilobytes)}</dd>
				<dt>Проверка обновлений</dt>
				<dd>{formatDateTime(status.updateCheckedAt)}</dd>
				{#if status.lastUpgradeResult}
					<dt>Последнее обновление</dt>
					<dd>{status.lastUpgradeResult}</dd>
				{/if}
			</dl>
			{#if status.updateAvailable && status.packageInstalled}
				<p>
					<button disabled={upgrading} onclick={() => upgrade(status.latestVersion)}>
						Обновить до {status.latestVersion}
					</button>
				</p>
			{/if}
		{:catch error}
			<p class="error">{errorMessage(error)}</p>
		{/await}
	</section>

	<section class="card">
		<h2>Xray</h2>
		{#await xray}
			<p class="muted">Загрузка…</p>
		{:then status}
			<dl class="props">
				<dt>Состояние</dt>
				<dd>{status.installed ? status.state : 'не установлен'}</dd>
				<dt>Версия</dt>
				<dd>
					{status.version ?? '—'}
					{#if status.updateAvailable}<span class="warn">→ {status.latestVersion}</span>{/if}
				</dd>
				<dt>Перезапусков</dt>
				<dd>{status.restarts}</dd>
				{#if status.lastError}
					<dt>Ошибка</dt>
					<dd class="error">{status.lastError}</dd>
				{/if}
			</dl>
			{#await system then sys}
				<p class="muted">Память: {formatKilobytes(sys.xrayMemoryRssKilobytes)}</p>
			{/await}
		{:catch error}
			<p class="error">{errorMessage(error)}</p>
		{/await}
	</section>

	<section class="card">
		<h2>Роутер</h2>
		{#await keenetic}
			<p class="muted">Загрузка…</p>
		{:then info}
			<dl class="props">
				<dt>Модель</dt>
				<dd>{info.model}</dd>
				<dt>KeeneticOS</dt>
				<dd>{info.release}</dd>
				<dt>Совместимость</dt>
				<dd class={info.isSupported ? 'ok' : 'error'}>{info.isSupported ? 'да' : 'нет'}</dd>
			</dl>
			{#each info.problems as problem (problem)}
				<p class="error">{problem}</p>
			{/each}
		{:catch error}
			<p class="error">{errorMessage(error)}</p>
		{/await}
	</section>
</div>

<style>
	.warn {
		color: var(--warn);
	}

	.ok {
		color: var(--ok);
	}

	section p:last-child {
		margin-bottom: 0;
	}
</style>
