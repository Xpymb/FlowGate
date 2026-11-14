<script lang="ts">
	import { untrack } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { formatBytes, formatDateTime, hostOf, toNumber } from '#lib/format.js';
	import { hotkey } from '#lib/hotkeys.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { hoursLabel, intervalOptions, label, refreshStatuses } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { matchesQuery, UrlParam } from '#lib/table.svelte.js';
	import { attempt, attemptOk, copyText } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Field from '#lib/components/Field.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import Menu from '#lib/components/Menu.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SearchInput from '#lib/components/SearchInput.svelte';
	import UpdateButton from '#lib/components/UpdateButton.svelte';

	type Subscription = components['schemas']['SubscriptionDto'];
	type SaveRequest = components['schemas']['SubscriptionSaveRequest'];
	type UpdateVia = components['schemas']['UpdateVia'];

	const subscriptions = new Resource(() => unwrap(api.GET('/api/subscriptions')), {
		live: ['subscriptions', 'servers']
	});
	const query = new UrlParam('q');

	let editorOpen = $state(false);
	let editingId = $state<string>();
	let form = $state(emptyForm());
	let saving = $state(false);
	let refreshing = $state<Record<string, boolean>>({});
	let advanced = $state(false);

	const visible = $derived(
		(subscriptions.data ?? []).filter((item) =>
			matchesQuery(query.value, [item.displayName, hostOf(item.url)])
		)
	);

	$effect(() => hotkey('n', openCreate));

	$effect(() => {
		if (page.url.searchParams.get('new') !== '1') return;
		untrack(() => {
			openCreate();
			void goto(resolve('/subscriptions'), { replace: true, reset: false });
		});
	});

	function emptyForm() {
		return {
			url: '',
			name: '',
			autoUpdate: true,
			interval: '',
			userAgent: '',
			sendHwid: false,
			sendDeviceInfo: false
		};
	}

	function openCreate() {
		editingId = undefined;
		form = emptyForm();
		advanced = false;
		editorOpen = true;
	}

	function openEdit(item: Subscription) {
		editingId = item.id;
		form = {
			url: item.url,
			name: item.name ?? '',
			autoUpdate: item.autoUpdate,
			interval: item.updateIntervalHours?.toString() ?? '',
			userAgent: item.userAgent ?? '',
			sendHwid: item.sendHwid,
			sendDeviceInfo: item.sendDeviceInfo
		};
		advanced = !!(item.userAgent || item.sendHwid || item.sendDeviceInfo);
		editorOpen = true;
	}

	async function save() {
		if (!form.url.trim()) return;
		const body: SaveRequest = {
			url: form.url.trim(),
			name: form.name.trim() || null,
			autoUpdate: form.autoUpdate,
			updateIntervalHours: form.interval ? Number(form.interval) : null,
			userAgent: form.userAgent.trim() || null,
			sendHwid: form.sendHwid,
			sendDeviceInfo: form.sendDeviceInfo
		};
		saving = true;
		const id = editingId;
		const result = await attempt(
			() =>
				id
					? unwrap(api.PUT('/api/subscriptions/{id}', { params: { path: { id } }, body }))
					: unwrap(api.POST('/api/subscriptions', { body })),
			id ? 'Подписка сохранена' : 'Подписка добавлена'
		);
		saving = false;
		if (!result) return;
		editorOpen = false;
		await subscriptions.reload();
		if (result.refreshJobId) {
			await follow(
				result.id,
				api.GET('/api/jobs/{id}', { params: { path: { id: result.refreshJobId } } })
			);
		}
	}

	async function refresh(id: string, via: UpdateVia) {
		await follow(
			id,
			api.POST('/api/subscriptions/{id}/refresh', { params: { path: { id }, query: { via } } })
		);
	}

	async function follow(id: string, start: Parameters<typeof trackJob>[0]) {
		refreshing[id] = true;
		await trackJob(start);
		refreshing[id] = false;
		await subscriptions.reload();
	}

	async function remove(item: Subscription) {
		const ok = await ask({
			title: `Удалить подписку «${item.displayName}»?`,
			message: `Вместе с ней удалятся её серверы (${item.serverCount}). Соединения через них перестанут работать.`,
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		const done = await attemptOk(
			() => unwrap(api.DELETE('/api/subscriptions/{id}', { params: { path: { id: item.id } } })),
			'Подписка удалена'
		);
		if (done) void subscriptions.reload();
	}

	function usage(item: Subscription): { used: number; total?: number; share?: number } | undefined {
		const provider = item.provider;
		if (!provider) return undefined;
		const used = (toNumber(provider.uploadBytes) ?? 0) + (toNumber(provider.downloadBytes) ?? 0);
		const total = toNumber(provider.totalBytes) || undefined;
		if (!used && !total) return undefined;
		return { used, total, share: total ? Math.min(used / total, 1) : undefined };
	}

	function daysLeft(value: string | null | undefined): number | undefined {
		if (!value) return undefined;
		const ms = new Date(value).getTime() - Date.now();
		return Number.isNaN(ms) ? undefined : Math.ceil(ms / 86_400_000);
	}
</script>

<PageHeader
	title="Подписки"
	description="Ссылки провайдеров, из которых FlowGate сам получает и обновляет серверы."
>
	{#snippet actions()}
		<button class="btn primary" onclick={openCreate} title="Клавиша N">
			<Icon name="plus" size={16} />Добавить подписку
		</button>
	{/snippet}
</PageHeader>

<Loadable resource={subscriptions} rows={3}>
	{#snippet children(list)}
		{#if list.length === 0}
			<EmptyState
				icon="subscription"
				title="Подписок пока нет"
				text="Вставьте адрес подписки провайдера — серверы появятся сами и будут обновляться по расписанию."
			>
				<button class="btn primary" onclick={openCreate}>Добавить подписку</button>
			</EmptyState>
		{:else}
			{#if list.length > 3}
				<div class="toolbar">
					<SearchInput
						value={query.value}
						placeholder="Имя или адрес"
						oninput={(value) => query.set(value)}
					/>
				</div>
			{/if}
			<div class="list">
				{#each visible as item (item.id)}
					{@const last = item.lastRefresh}
					{@const status = label(refreshStatuses, last?.status)}
					{@const traffic = usage(item)}
					{@const days = daysLeft(item.provider?.expireAt)}
					<article class="panel sub">
						<div class="head">
							<div class="title">
								<h2>{item.displayName}</h2>
								<span class="muted small mono">{hostOf(item.url)}</span>
							</div>
							{#if last}<Badge tone={status.tone} dot>{status.label}</Badge>{/if}
							<div class="row">
								<UpdateButton
									small
									label="Обновить"
									busy={refreshing[item.id]}
									onrun={(via) => refresh(item.id, via)}
								/>
								<Menu
									ariaLabel="Действия с подпиской {item.displayName}"
									items={[
										{ label: 'Изменить', icon: 'edit', onclick: () => openEdit(item) },
										{
											label: 'Скопировать адрес',
											icon: 'copy',
											onclick: () => copyText(item.url, 'Адрес подписки скопирован')
										},
										{
											label: 'Показать серверы',
											icon: 'server',
											href: resolve(`/servers?q=${encodeURIComponent(item.displayName)}`)
										},
										'separator',
										{ label: 'Удалить', icon: 'trash', danger: true, onclick: () => remove(item) }
									]}
								/>
							</div>
						</div>

						<div class="facts">
							<div>
								<span class="fact-label">Серверов</span>
								<span class="fact-value num">{item.serverCount}</span>
							</div>
							{#if traffic}
								<div class="traffic">
									<span class="fact-label">Трафик</span>
									<span class="fact-value num">
										{formatBytes(traffic.used)}{#if traffic.total}<span class="muted">
												из {formatBytes(traffic.total)}</span
											>{/if}
									</span>
									{#if traffic.share !== undefined}
										<span
											class="meter"
											class:high={traffic.share > 0.85}
											role="meter"
											aria-valuemin="0"
											aria-valuemax="100"
											aria-valuenow={Math.round(traffic.share * 100)}
											aria-label="Израсходовано трафика"
										>
											<span style:width="{traffic.share * 100}%"></span>
										</span>
									{/if}
								</div>
							{/if}
							{#if item.provider?.expireAt}
								<div>
									<span class="fact-label">Действует до</span>
									<span class="fact-value" class:soon={days !== undefined && days <= 7}>
										{new Date(item.provider.expireAt).toLocaleDateString('ru-RU')}
										{#if days !== undefined}<span class="muted small">
												· {days > 0 ? `${days} дн.` : 'истекла'}</span
											>{/if}
									</span>
								</div>
							{/if}
							<div>
								<span class="fact-label">Обновлена</span>
								<span class="fact-value">{formatDateTime(last?.at)}</span>
							</div>
							<div>
								<span class="fact-label">Следующее</span>
								<span class="fact-value">
									{item.autoUpdate ? formatDateTime(item.nextRefreshAt) : 'вручную'}
									{#if item.autoUpdate}<span class="muted small">
											· раз в {hoursLabel(toNumber(item.effectiveIntervalHours) ?? 24)}</span
										>{/if}
								</span>
							</div>
						</div>

						{#if last?.error}<p class="error-text small">{last.error}</p>{/if}
						{#if last && (toNumber(last.rejected) ?? 0) > 0}
							<p class="muted small">
								Узлов в ответе: {last.received}, принято {last.accepted}, отклонено {last.rejected}.
							</p>
						{/if}
						{#if item.provider?.announce}
							<p class="announce small"><Icon name="info" size={14} />{item.provider.announce}</p>
						{/if}
					</article>
				{:else}
					<EmptyState icon="search" title="Ничего не нашлось" text="Измените запрос поиска.">
						<button class="btn" onclick={() => query.set('')}>Сбросить поиск</button>
					</EmptyState>
				{/each}
			</div>
		{/if}
	{/snippet}
</Loadable>

<Dialog
	bind:open={editorOpen}
	variant="side"
	title={editingId ? 'Подписка' : 'Новая подписка'}
	description={editingId ? undefined : 'Серверы загрузятся сразу после сохранения.'}
	onsubmit={save}
>
	<div class="form">
		<Field
			label="Адрес подписки"
			hint="Содержит ваш токен доступа — не показывайте его посторонним."
		>
			<input bind:value={form.url} placeholder="https://…" type="url" spellcheck="false" />
		</Field>
		<Field label="Имя" hint="Пусто — возьмём имя, которое сообщает провайдер.">
			<input bind:value={form.name} placeholder="Например, Мой VPN" />
		</Field>
		<label class="check">
			<input type="checkbox" bind:checked={form.autoUpdate} />
			<span>Обновлять по расписанию</span>
		</label>
		{#if form.autoUpdate}
			<Field label="Как часто">
				<select bind:value={form.interval}>
					<option value="">как советует провайдер или по умолчанию</option>
					{#each intervalOptions(form.interval) as hours (hours)}
						<option value={hours.toString()}>раз в {hoursLabel(hours)}</option>
					{/each}
				</select>
			</Field>
		{/if}

		<button class="disclosure" aria-expanded={advanced} onclick={() => (advanced = !advanced)}>
			<Icon name={advanced ? 'chevronDown' : 'chevronRight'} size={16} />
			Для требовательных провайдеров
		</button>
		{#if advanced}
			<Field
				label="User-Agent"
				hint="Некоторые провайдеры отдают Xray-JSON только клиентам вроде Happ."
			>
				<input bind:value={form.userAgent} placeholder="FlowGate/<версия>" />
			</Field>
			<label class="check">
				<input type="checkbox" bind:checked={form.sendHwid} />
				<span
					>Отправлять x-hwid<span class="hint">Идентификатор устройства для лимита устройств</span
					></span
				>
			</label>
			<label class="check">
				<input type="checkbox" bind:checked={form.sendDeviceInfo} />
				<span>Сообщать модель и версию роутера</span>
			</label>
		{/if}
	</div>
	{#snippet actions()}
		<button class="btn" onclick={() => (editorOpen = false)}>Отмена</button>
		<button class="btn primary" disabled={!form.url.trim()} aria-busy={saving} onclick={save}>
			{editingId ? 'Сохранить' : 'Добавить подписку'}
		</button>
	{/snippet}
</Dialog>

<style>
	.toolbar {
		margin-bottom: 16px;
	}

	.list {
		display: grid;
		gap: 16px;
	}

	.sub {
		display: grid;
		gap: 16px;
		padding: 18px 20px;
	}

	.head {
		display: flex;
		align-items: center;
		gap: 12px;
		flex-wrap: wrap;
	}

	.title {
		display: grid;
		flex: 1 1 240px;
		min-width: 0;
	}

	.facts {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
		gap: 16px 24px;
		padding-top: 16px;
		border-top: 1px solid var(--line);
	}

	.facts > div {
		display: grid;
		gap: 2px;
		align-content: start;
	}

	.traffic {
		grid-column: span 2;
	}

	.fact-label {
		color: var(--muted);
		font-size: 12.5px;
	}

	.fact-value {
		font-weight: 500;
	}

	.fact-value.soon {
		color: var(--warn);
	}

	.meter {
		display: block;
		height: 6px;
		margin-top: 6px;
		border-radius: 999px;
		background: var(--hover);
		overflow: hidden;
	}

	.meter span {
		display: block;
		height: 100%;
		border-radius: inherit;
		background: var(--accent);
	}

	.meter.high span {
		background: var(--warn);
	}

	.announce {
		display: flex;
		gap: 8px;
		padding: 10px 12px;
		border-radius: var(--r-sm);
		background: var(--info-soft);
		color: var(--ink-2);
	}

	.announce :global(.icon) {
		margin-top: 2px;
	}

	.disclosure {
		display: flex;
		align-items: center;
		gap: 6px;
		color: var(--accent-text);
		font-weight: 500;
		justify-self: start;
	}

	@media (max-width: 640px) {
		.traffic {
			grid-column: auto;
		}
	}
</style>
