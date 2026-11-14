<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { formatDateTime, toNumber } from '#lib/format.js';
	import { hoursLabel, intervalOptions, label, updateProxyStates } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { attempt } from '#lib/toast.svelte.js';
	import { guardUnsaved } from '#lib/unsaved.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import Field from '#lib/components/Field.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SaveBar from '#lib/components/SaveBar.svelte';

	type Settings = components['schemas']['SettingsDto'];
	type Interval = components['schemas']['UpdateIntervalDto'];
	type SaveRequest = components['schemas']['SettingsSaveRequest'];
	type IntervalKey =
		'subscriptionDefault' | 'domainSourceRefresh' | 'xrayUpdateCheck' | 'systemUpdateCheck';
	type Form = ReturnType<typeof toForm>;

	const intervalFields: { key: IntervalKey; title: string; hint: string }[] = [
		{
			key: 'subscriptionDefault',
			title: 'Обновление подписок',
			hint: 'Если у подписки нет своего интервала и провайдер его не сообщает.'
		},
		{
			key: 'domainSourceRefresh',
			title: 'Обновление источников списков',
			hint: 'Для источников с включённым обновлением по расписанию.'
		},
		{
			key: 'xrayUpdateCheck',
			title: 'Проверка новых версий Xray',
			hint: 'Только проверка — ставите вы.'
		},
		{
			key: 'systemUpdateCheck',
			title: 'Проверка новых версий FlowGate',
			hint: 'Только проверка — обновление запускаете с обзора.'
		}
	];

	let form = $state<Form>();
	let savedForm = $state<Form>();
	let saving = $state(false);

	const settings = new Resource(async () => {
		const loaded = await unwrap(api.GET('/api/settings'));
		// Форму не живим: событие изменения затёрло бы несохранённые правки.
		if (!dirty) {
			form = toForm(loaded);
			savedForm = toForm(loaded);
		}
		return loaded;
	});
	const servers = new Resource(() => unwrap(api.GET('/api/servers')), { live: ['servers'] });
	const status = new Resource(() => unwrap(api.GET('/api/settings/update-proxy/status')), {
		live: ['settings']
	});

	const dirty = $derived(
		!!form && !!savedForm && JSON.stringify(form) !== JSON.stringify(savedForm)
	);
	const invalid = $derived(
		form?.proxyEnabled && !form.serverId ? 'Выберите сервер для прокси обновлений' : undefined
	);
	const proxyState = $derived(
		status.data ? label(updateProxyStates, status.data.state) : undefined
	);

	guardUnsaved(() => dirty && !saving);

	$effect(() => {
		const timer = setInterval(() => void status.reload(), 5000);
		return () => clearInterval(timer);
	});

	function toForm(value: Settings) {
		const hours = (interval: Interval) => toNumber(interval.hours)?.toString() ?? '';
		return {
			xrayLogLevel: value.xrayLogLevel,
			proxyEnabled: value.updateProxy.enabled,
			serverId: value.updateProxy.serverId ?? '',
			extraDomains: value.updateProxy.extraDomains.join('\n'),
			intervals: {
				subscriptionDefault: hours(value.intervals.subscriptionDefault),
				domainSourceRefresh: hours(value.intervals.domainSourceRefresh),
				xrayUpdateCheck: hours(value.intervals.xrayUpdateCheck),
				systemUpdateCheck: hours(value.intervals.systemUpdateCheck)
			} as Record<IntervalKey, string>
		};
	}

	async function save() {
		if (!form || invalid) return;
		const current = form;
		const hours = (key: IntervalKey) =>
			current.intervals[key] ? Number(current.intervals[key]) : null;
		const body: SaveRequest = {
			xrayLogLevel: current.xrayLogLevel,
			updateProxy: {
				enabled: current.proxyEnabled,
				serverId: current.serverId || null,
				extraDomains: current.extraDomains
					.split(/[\s,]+/)
					.map((domain) => domain.trim())
					.filter(Boolean)
			},
			intervals: {
				subscriptionDefaultHours: hours('subscriptionDefault'),
				domainSourceRefreshHours: hours('domainSourceRefresh'),
				xrayUpdateCheckHours: hours('xrayUpdateCheck'),
				systemUpdateCheckHours: hours('systemUpdateCheck')
			}
		};
		saving = true;
		const saved = await attempt(
			() => unwrap(api.PUT('/api/settings', { body })),
			'Настройки сохранены'
		);
		saving = false;
		if (!saved) return;
		form = toForm(saved);
		savedForm = toForm(saved);
		settings.data = saved;
		void status.reload();
	}

	function reset() {
		if (savedForm) form = JSON.parse(JSON.stringify(savedForm)) as Form;
	}
</script>

<PageHeader
	title="Настройки"
	description="Как FlowGate загружает обновления и как часто их проверяет."
/>

<Loadable resource={settings} rows={6}>
	{#snippet children(current)}
		{#if form}
			<div class="stack">
				<section class="panel">
					<header class="panel-head">
						<h2>Прокси для обновлений</h2>
						{#if proxyState}<Badge tone={proxyState.tone} dot>{proxyState.label}</Badge>{/if}
					</header>
					<div class="panel-body">
						<p class="lead">
							Подписки, источники списков, выпуски Xray и сам FlowGate скачиваются через отдельный
							Xray с выбранным сервером — на случай, если GitHub или провайдер недоступны напрямую.
							Он запускается при первой загрузке и гаснет после простоя, основной Xray не трогает.
							Если прокси не отвечает, загрузка идёт напрямую.
						</p>
						<Field label="Загружать через прокси" layout="split" group>
							<label class="check">
								<input type="checkbox" bind:checked={form.proxyEnabled} />
								<span>{form.proxyEnabled ? 'Включено' : 'Выключено'}</span>
							</label>
						</Field>
						<Field label="Сервер" layout="split" hint="Через него пойдут загрузки." error={invalid}>
							<select bind:value={form.serverId} disabled={!form.proxyEnabled}>
								<option value="">— не выбран —</option>
								{#each servers.data ?? [] as server (server.id)}
									<option value={server.id}
										>{server.name} ({server.protocol}, {server.address})</option
									>
								{/each}
							</select>
						</Field>
						<Field
							label="Свои домены"
							layout="split"
							hint="По одному в строке: example.com — с поддоменами, full:host — только этот хост. Хосты подписок и ссылок источников добавляются сами."
						>
							<textarea
								bind:value={form.extraDomains}
								rows="4"
								disabled={!form.proxyEnabled}
								placeholder="example.com&#10;full:cdn.example.org"></textarea>
							{#snippet extra()}
								<details class="builtin">
									<summary>Встроенные домены: {current.updateProxy.builtInDomains.length}</summary>
									<div class="domains">
										{#each current.updateProxy.builtInDomains as domain (domain)}
											<code>{domain}</code>
										{/each}
									</div>
								</details>
							{/snippet}
						</Field>
						{#if status.data}
							<div class="proxy-status">
								<span>Порт <code>127.0.0.1:{status.data.port}</code></span>
								{#if status.data.processId}
									<span>pid <code>{status.data.processId}</code></span>
									<span>запущен {formatDateTime(status.data.startedAt)}</span>
									<span>загрузок сейчас: {status.data.activeRequests}</span>
								{/if}
							</div>
							{#if status.data.reason}<p class="error-text small">{status.data.reason}</p>{/if}
						{/if}
					</div>
				</section>

				<section class="panel">
					<header class="panel-head"><h2>Расписание</h2></header>
					<div class="panel-body">
						{#each intervalFields as field (field.key)}
							{@const interval = current.intervals[field.key]}
							<Field label={field.title} hint={field.hint} layout="split">
								<select bind:value={form.intervals[field.key]}>
									<option value="">
										по умолчанию — раз в {hoursLabel(toNumber(interval.defaultHours) ?? 24)}
									</option>
									{#each intervalOptions(form.intervals[field.key]) as hours (hours)}
										<option value={hours.toString()}>раз в {hoursLabel(hours)}</option>
									{/each}
								</select>
							</Field>
						{/each}
					</div>
				</section>

				<section class="panel">
					<header class="panel-head"><h2>Xray</h2></header>
					<div class="panel-body">
						<Field
							label="Подробность журнала"
							layout="split"
							hint="При смене конфигурация пересобирается, Xray перезапускается — связь пропадёт на пару секунд."
						>
							<select bind:value={form.xrayLogLevel}>
								{#each current.xrayLogLevels as level (level)}
									<option value={level}>{level}</option>
								{/each}
							</select>
						</Field>
					</div>
				</section>
			</div>

			<SaveBar {dirty} {saving} {invalid} onsave={save} onreset={reset} />
		{/if}
	{/snippet}
</Loadable>

<style>
	.lead {
		margin-bottom: 20px;
		color: var(--muted);
	}

	.builtin summary {
		color: var(--accent-text);
		font-size: 12.5px;
		cursor: pointer;
	}

	.domains {
		display: flex;
		flex-wrap: wrap;
		gap: 4px 12px;
		margin-top: 8px;
	}

	.domains code {
		font-size: 12px;
		color: var(--muted);
	}

	.proxy-status {
		display: flex;
		flex-wrap: wrap;
		gap: 6px 20px;
		margin-top: 4px;
		padding: 12px 14px;
		border-radius: var(--r-sm);
		background: var(--surface-2);
		color: var(--muted);
		font-size: 12.5px;
	}
</style>
