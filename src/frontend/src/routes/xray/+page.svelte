<script lang="ts">
	import { tick } from 'svelte';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { formatBytes, formatDateTime, formatDuration, formatKilobytes } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { label, xrayStates } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { UrlParam } from '#lib/table.svelte.js';
	import { attempt } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SearchInput from '#lib/components/SearchInput.svelte';
	import UpdateButton from '#lib/components/UpdateButton.svelte';

	type UpdateVia = components['schemas']['UpdateVia'];
	type Action = 'start' | 'stop' | 'restart';

	const status = new Resource(() => unwrap(api.GET('/api/xray')), { live: ['xray'] });
	const system = new Resource(() => unwrap(api.GET('/api/system')), { live: ['xray'] });
	const tab = new UrlParam('tab');

	let includePrerelease = $state(false);
	const releases = new Resource(() =>
		unwrap(api.GET('/api/xray/releases', { params: { query: { includePrerelease } } }))
	);
	let logTail = $state(200);
	const logs = new Resource(() =>
		unwrap(api.GET('/api/xray/logs', { params: { query: { tail: logTail } } }))
	);

	let busy = $state<Action | 'install'>();
	let installing = $state<string | null>();
	let follow = $state(true);
	let logFilter = $state('');
	let checking = $state(false);
	let logBox = $state<HTMLPreElement>();
	/** Порядок журнала: новые строки сверху (по умолчанию) или снизу. Выбор помнит браузер. */
	let newestFirst = $state(readOrder());

	const xrayState = $derived(status.data ? label(xrayStates, status.data.state) : undefined);
	const running = $derived(status.data?.state === 'Running' || status.data?.state === 'Starting');
	const logLines = $derived(filterLines(logs.data?.lines ?? []));

	// Живой журнал: перечитываем, пока вкладка открыта и включено слежение.
	$effect(() => {
		if (!follow || tab.value !== 'log') return;
		const timer = setInterval(() => void logs.reload(), 3000);
		return () => clearInterval(timer);
	});

	// Новые строки держим в поле зрения, если пользователь и так у края с новыми строками.
	$effect(() => {
		void logLines.length;
		const box = logBox;
		if (!box || !follow) return;
		if (newestFirst) {
			if (box.scrollTop < 80) void tick().then(() => (box.scrollTop = 0));
			return;
		}
		const nearBottom = box.scrollHeight - box.scrollTop - box.clientHeight < 80;
		if (nearBottom || box.scrollTop === 0)
			void tick().then(() => (box.scrollTop = box.scrollHeight));
	});

	function readOrder(): boolean {
		try {
			return localStorage.getItem('flowgate-log-order') !== 'oldest-first';
		} catch {
			return true;
		}
	}

	function setOrder(value: boolean) {
		newestFirst = value;
		try {
			if (value) localStorage.removeItem('flowgate-log-order');
			else localStorage.setItem('flowgate-log-order', 'oldest-first');
		} catch {
			// без хранилища выбор живёт до перезагрузки
		}
	}

	async function control(action: Action) {
		if (action === 'stop') {
			const ok = await ask({
				title: 'Остановить Xray?',
				message: 'Трафик через все соединения будет прерван до запуска Xray.',
				action: 'Остановить',
				danger: true
			});
			if (!ok) return;
		}
		busy = action;
		const titles = { start: 'Xray запущен', stop: 'Xray остановлен', restart: 'Xray перезапущен' };
		const result = await attempt(() => unwrap(api.POST(`/api/xray/${action}`)), titles[action]);
		if (result) status.data = result;
		busy = undefined;
		void logs.reload();
	}

	async function install(version: string | null, via: UpdateVia) {
		busy = 'install';
		installing = version;
		const ok = await trackJob(
			api.POST('/api/xray/install', { params: { query: { via } }, body: { version } })
		);
		busy = undefined;
		installing = undefined;
		if (ok) await Promise.all([status.reload(), releases.reload(), logs.reload()]);
	}

	async function checkGitHub() {
		checking = true;
		await attempt(async () => {
			releases.data = await unwrap(
				api.GET('/api/xray/releases', { params: { query: { includePrerelease, refresh: true } } })
			);
		});
		checking = false;
	}

	function filterLines<T extends { text: string }>(lines: T[]): T[] {
		const needle = logFilter.trim().toLowerCase();
		return needle ? lines.filter((line) => line.text.toLowerCase().includes(needle)) : lines;
	}

	function levelOf(text: string): 'error' | 'warning' | undefined {
		if (/\[(error|fatal)\]/i.test(text)) return 'error';
		if (/\[warning\]/i.test(text)) return 'warning';
		return undefined;
	}
</script>

<PageHeader title="Ядро Xray" description="Управление процессом Xray, версиями и журналом.">
	{#snippet meta()}
		{#if xrayState}<Badge tone={xrayState.tone} dot>{xrayState.label}</Badge>{/if}
	{/snippet}
	{#snippet actions()}
		{#if status.data?.installed}
			{#if running}
				<button
					class="btn"
					disabled={!!busy}
					aria-busy={busy === 'stop'}
					onclick={() => control('stop')}
				>
					<Icon name="stop" size={15} />Остановить
				</button>
				<button
					class="btn primary"
					disabled={!!busy}
					aria-busy={busy === 'restart'}
					onclick={() => control('restart')}
				>
					<Icon name="refresh" size={16} />Перезапустить
				</button>
			{:else}
				<button
					class="btn primary"
					disabled={!!busy}
					aria-busy={busy === 'start'}
					onclick={() => control('start')}
				>
					<Icon name="play" size={15} />Запустить
				</button>
			{/if}
		{/if}
	{/snippet}
</PageHeader>

<Loadable resource={status} rows={2}>
	{#snippet children(xray)}
		{#if !xray.installed}
			<EmptyState
				icon="download"
				title="Xray не установлен"
				text="Для работы соединений требуется Xray. Последняя версия будет загружена с GitHub и запущена."
			>
				<UpdateButton
					primary
					label="Установить последнюю версию"
					busy={busy === 'install'}
					onrun={(via) => install(null, via)}
				/>
			</EmptyState>
		{:else}
			{#if xray.updateAvailable}
				<div class="banner" role="status">
					<Icon name="download" size={18} />
					<span>
						Доступна версия <b>{xray.latestVersion}</b>. При обновлении Xray будет перезапущен,
						соединения прервутся на несколько секунд.
					</span>
					<UpdateButton
						small
						primary
						label="Обновить"
						busy={busy === 'install' && installing === null}
						disabled={!!busy}
						onrun={(via) => install(null, via)}
					/>
				</div>
			{/if}
			{#if xray.lastError}
				<div class="banner danger" role="alert">
					<Icon name="alert" size={18} />
					<span><b>Последняя ошибка:</b> {xray.lastError}</span>
				</div>
			{/if}
			<div class="facts panel">
				<div>
					<span class="fact-label">Версия</span>
					<span class="fact-value mono">{xray.version ?? '—'}</span>
				</div>
				<div>
					<span class="fact-label">Запущен</span>
					<span class="fact-value">{running ? formatDateTime(xray.startedAt) : '—'}</span>
				</div>
				<div>
					<span class="fact-label">Память</span>
					<span class="fact-value">{formatKilobytes(system.data?.xrayMemoryRssKilobytes)}</span>
				</div>
				<div>
					<span class="fact-label">Перезапусков</span>
					<span class="fact-value num">{xray.restarts}</span>
				</div>
				<div>
					<span class="fact-label">Процесс</span>
					<span class="fact-value mono">{xray.processId ?? '—'}</span>
				</div>
				<div>
					<span class="fact-label">Автозапуск</span>
					<span class="fact-value">{xray.autostart ? 'включён' : 'выключен'}</span>
				</div>
			</div>
		{/if}
	{/snippet}
</Loadable>

<div class="tabs" role="tablist" aria-label="Разделы ядра">
	<button role="tab" aria-selected={tab.value !== 'log'} onclick={() => tab.set('')}>
		<Icon name="download" size={16} />Версии
	</button>
	<button role="tab" aria-selected={tab.value === 'log'} onclick={() => tab.set('log')}>
		<Icon name="log" size={16} />Журнал
	</button>
</div>

{#if tab.value === 'log'}
	<div class="panel" role="tabpanel" aria-label="Журнал">
		<div class="panel-head">
			<SearchInput bind:value={logFilter} placeholder="Фильтр строк" label="Фильтр журнала" />
			<div class="row right">
				<div class="segmented" role="group" aria-label="Порядок строк">
					<button aria-pressed={newestFirst} onclick={() => setOrder(true)}>
						<Icon name="arrowUp" size={13} />Новые сверху
					</button>
					<button aria-pressed={!newestFirst} onclick={() => setOrder(false)}>
						<Icon name="arrowDown" size={13} />Новые снизу
					</button>
				</div>
				<label class="check small">
					<input type="checkbox" bind:checked={follow} />
					<span>Автообновление</span>
				</label>
				<select
					bind:value={logTail}
					onchange={() => logs.reload()}
					class="tail"
					aria-label="Сколько строк"
				>
					<option value={100}>100 строк</option>
					<option value={200}>200 строк</option>
					<option value={500}>500 строк</option>
				</select>
				<button class="btn sm icon" aria-label="Обновить журнал" onclick={() => logs.reload()}>
					<Icon name="refresh" size={15} />
				</button>
			</div>
		</div>
		<Loadable resource={logs}>
			{#snippet children(log)}
				{@const filtered = filterLines(log.lines)}
				{@const lines = newestFirst ? [...filtered].reverse() : filtered}
				{#if lines.length === 0}
					<EmptyState
						bare
						icon="log"
						title={logFilter ? 'Нет записей, соответствующих фильтру' : 'Журнал пуст'}
						text={logFilter ? undefined : 'Записи появятся после запуска Xray.'}
					/>
				{:else}
					<pre class="log" bind:this={logBox}>{#each lines as line, index (index)}<span
								class="line {levelOf(line.text) ?? ''}"
								>{line.text}
</span>{/each}</pre>
				{/if}
			{/snippet}
		</Loadable>
	</div>
{:else}
	<div class="panel" role="tabpanel" aria-label="Версии">
		<div class="panel-head">
			<h2>Выпуски на GitHub</h2>
			<label class="check small">
				<input
					type="checkbox"
					bind:checked={includePrerelease}
					onchange={() => releases.reload()}
				/>
				<span>Показывать предварительные</span>
			</label>
			<button class="btn sm" aria-busy={checking} onclick={checkGitHub}>
				<Icon name="refresh" size={14} />Проверить обновления
			</button>
		</div>
		<Loadable resource={releases}>
			{#snippet children(list)}
				{#if list.length === 0}
					<EmptyState
						bare
						icon="download"
						title="Выпуски не найдены"
						text="Проверьте доступ к GitHub или настройте прокси обновлений."
					/>
				{:else}
					<div class="table-scroll">
						<table class="table">
							<thead>
								<tr>
									<th>Версия</th>
									<th>Опубликована</th>
									<th class="num">Размер</th>
									<th class="actions-cell"><span class="visually-hidden">Действия</span></th>
								</tr>
							</thead>
							<tbody>
								{#each list as release (release.tag)}
									<tr>
										<td>
											<span class="mono version">{release.version}</span>
											{#if release.isLatest}<Badge tone="accent">последняя</Badge>{/if}
											{#if release.isPrerelease}<Badge tone="warn">предварительная</Badge>{/if}
										</td>
										<td class="muted">{formatDateTime(release.publishedAt)}</td>
										<td class="num muted">{formatBytes(release.sizeBytes)}</td>
										<td class="actions-cell">
											{#if release.isInstalled}
												<Badge tone="ok" dot>установлена</Badge>
											{:else}
												<UpdateButton
													small
													label="Установить"
													busy={busy === 'install' && installing === release.version}
													disabled={!!busy}
													onrun={(via) => install(release.version, via)}
												/>
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
	</div>
{/if}

<p class="muted small footnote">
	Время работы FlowGate: {formatDuration(system.data?.uptimeSeconds)}. Проверка новых версий Xray
	выполняется по расписанию, заданному в настройках.
</p>

<style>
	.banner {
		display: flex;
		align-items: center;
		gap: 12px;
		margin-bottom: 16px;
		padding: 12px 12px 12px 16px;
		border-radius: var(--r-md);
		background: var(--accent-soft);
		color: var(--ink-2);
	}

	.banner > :global(.icon) {
		color: var(--accent-text);
	}

	.banner span {
		flex: 1;
	}

	.banner.danger {
		background: var(--danger-soft);
	}

	.banner.danger > :global(.icon) {
		color: var(--danger);
	}

	.facts {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
	}

	.facts > div {
		display: grid;
		gap: 2px;
		padding: 16px 20px;
		border-left: 1px solid var(--line);
	}

	.facts > div:first-child {
		border-left: 0;
	}

	.fact-label {
		color: var(--muted);
		font-size: 12.5px;
	}

	.fact-value {
		font-size: 15px;
		font-weight: 500;
	}

	.tabs {
		display: flex;
		gap: 4px;
		margin: 32px 0 12px;
		border-bottom: 1px solid var(--line);
	}

	[role='tab'] {
		display: flex;
		align-items: center;
		gap: 8px;
		margin-bottom: -1px;
		padding: 10px 14px;
		border-bottom: 2px solid transparent;
		color: var(--muted);
		font-weight: 500;
	}

	[role='tab']:hover {
		color: var(--ink);
	}

	[role='tab'][aria-selected='true'] {
		border-bottom-color: var(--accent);
		color: var(--ink);
	}

	.right {
		margin-left: auto;
	}

	.tail {
		width: auto;
		min-height: 30px;
		padding-top: 4px;
		padding-bottom: 4px;
		font-size: 13px;
	}

	.log {
		max-height: 60vh;
		margin: 0;
		overflow: auto;
		padding: 14px 20px;
		font-size: 12.5px;
		line-height: 1.65;
		white-space: pre-wrap;
		word-break: break-all;
	}

	.line.warning {
		color: var(--warn);
	}

	.line.error {
		color: var(--danger);
	}

	.version {
		margin-right: 8px;
		font-size: 13.5px;
	}

	.footnote {
		margin-top: 16px;
	}

	@media (max-width: 640px) {
		.facts > div:nth-child(odd) {
			border-left: 0;
		}
	}
</style>
