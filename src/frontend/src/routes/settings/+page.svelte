<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import { formatDateTime, toNumber } from '#lib/format.js';
	import { hoursLabel, intervalOptions, label, updateProxyStates } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { attempt } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type Settings = components['schemas']['SettingsDto'];
	type Interval = components['schemas']['UpdateIntervalDto'];
	type SaveRequest = components['schemas']['SettingsSaveRequest'];
	type IntervalKey =
		'subscriptionDefault' | 'domainSourceRefresh' | 'xrayUpdateCheck' | 'systemUpdateCheck';

	const intervalFields: { key: IntervalKey; title: string }[] = [
		{ key: 'subscriptionDefault', title: 'Обновление подписок' },
		{ key: 'domainSourceRefresh', title: 'Обновление источников списков' },
		{ key: 'xrayUpdateCheck', title: 'Проверка новых версий Xray' },
		{ key: 'systemUpdateCheck', title: 'Проверка новых версий FlowGate' }
	];

	let form = $state<ReturnType<typeof toForm>>();
	let saving = $state(false);

	const settings = new Resource(async () => {
		const loaded = await unwrap(api.GET('/api/settings'));
		form = toForm(loaded);
		return loaded;
	});
	const servers = new Resource(() => unwrap(api.GET('/api/servers')));
	const status = new Resource(() => unwrap(api.GET('/api/settings/update-proxy/status')));

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
		if (!form) return;
		const hours = (key: IntervalKey) =>
			form!.intervals[key] ? Number(form!.intervals[key]) : null;
		const body: SaveRequest = {
			xrayLogLevel: form.xrayLogLevel,
			updateProxy: {
				enabled: form.proxyEnabled,
				serverId: form.serverId || null,
				extraDomains: form.extraDomains
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
		settings.data = saved;
		void status.reload();
	}
</script>

<div class="toolbar">
	<h1>Настройки</h1>
	<button disabled={saving || !form} onclick={save}>{saving ? 'Сохранение…' : 'Сохранить'}</button>
</div>

<Loadable resource={settings}>
	{#snippet children(current)}
		{#if form}
			<div class="stack">
				<section class="card">
					<header>
						<h2>Обновления через прокси</h2>
						{#if status.data}
							{@const state = label(updateProxyStates, status.data.state)}
							<Badge tone={state.tone}>{state.label}</Badge>
						{/if}
					</header>
					<p class="muted">
						Подписки, источники списков, релизы Xray и пакет FlowGate качаются через отдельный
						экземпляр Xray с выбранным сервером. Он запускается при первой загрузке и
						останавливается после нескольких минут простоя; основной Xray не затрагивается. Если
						прокси недоступен, загрузка идёт напрямую.
					</p>
					<div class="form">
						<label class="check">
							<input type="checkbox" bind:checked={form.proxyEnabled} /> Загружать обновления через прокси
						</label>
						<label>
							Сервер
							<select bind:value={form.serverId}>
								<option value="">— не выбран —</option>
								{#each servers.data ?? [] as server (server.id)}
									<option value={server.id}
										>{server.name} ({server.protocol}, {server.address})</option
									>
								{/each}
							</select>
						</label>
						<label>
							Свои домены
							<textarea
								bind:value={form.extraDomains}
								rows="4"
								placeholder="example.com&#10;full:cdn.example.org"></textarea>
							<span class="hint">
								По одному в строке: <code>example.com</code> — с поддоменами,
								<code>full:host</code> — только этот хост. Хосты подписок и прямых ссылок источников добавляются
								сами.
							</span>
						</label>
						<div>
							<span class="hint">Встроенные домены:</span>
							<ul class="plain domains">
								{#each current.updateProxy.builtInDomains as domain (domain)}
									<li><code>{domain}</code></li>
								{/each}
							</ul>
						</div>
					</div>
					{#if status.data}
						<dl class="props">
							<dt>Порт</dt>
							<dd>127.0.0.1:{status.data.port}</dd>
							{#if status.data.processId}
								<dt>Pid</dt>
								<dd>{status.data.processId}</dd>
								<dt>Запущен</dt>
								<dd>{formatDateTime(status.data.startedAt)}</dd>
								<dt>Загрузок сейчас</dt>
								<dd>{status.data.activeRequests}</dd>
							{/if}
						</dl>
						{#if status.data.reason}<p class="error">{status.data.reason}</p>{/if}
					{/if}
				</section>

				<section class="card">
					<h2>Интервалы обновлений</h2>
					<div class="form">
						{#each intervalFields as field (field.key)}
							{@const interval = current.intervals[field.key]}
							<label>
								{field.title}
								<select bind:value={form.intervals[field.key]}>
									<option value=""
										>по умолчанию ({hoursLabel(toNumber(interval.defaultHours) ?? 24)})</option
									>
									{#each intervalOptions(form.intervals[field.key]) as hours (hours)}
										<option value={hours.toString()}>{hoursLabel(hours)}</option>
									{/each}
								</select>
							</label>
						{/each}
						<span class="hint">
							Пустое поле — значение из конфигурации. У подписки может быть свой интервал или
							интервал от провайдера.
						</span>
					</div>
				</section>

				<section class="card">
					<h2>Xray</h2>
					<div class="form">
						<label>
							Уровень логирования
							<select bind:value={form.xrayLogLevel}>
								{#each current.xrayLogLevels as level (level)}
									<option value={level}>{level}</option>
								{/each}
							</select>
							<span class="hint">При смене конфигурация пересобирается, Xray перезапускается.</span>
						</label>
					</div>
				</section>
			</div>
		{/if}
	{/snippet}
</Loadable>

<style>
	header {
		display: flex;
		align-items: baseline;
		gap: 8px;
		margin-bottom: 8px;
	}

	header h2 {
		margin: 0 auto 0 0;
	}

	section p {
		margin: 0 0 12px;
	}

	.domains {
		display: flex;
		flex-wrap: wrap;
		gap: 4px 12px;
		margin-top: 4px;
	}

	dl.props {
		margin-top: 12px;
	}
</style>
