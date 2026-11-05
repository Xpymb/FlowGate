<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import Dialog from '#lib/components/Dialog.svelte';
	import Loadable from '#lib/components/Loadable.svelte';
	import UpdateButton from '#lib/components/UpdateButton.svelte';
	import { formatBytes, formatDateTime, hostOf, toNumber } from '#lib/format.js';
	import { trackJob } from '#lib/jobs.svelte.js';
	import { hoursLabel, intervalOptions, label, refreshStatuses } from '#lib/labels.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { attempt } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type Subscription = components['schemas']['SubscriptionDto'];
	type SaveRequest = components['schemas']['SubscriptionSaveRequest'];
	type UpdateVia = components['schemas']['UpdateVia'];

	const subscriptions = new Resource(() => unwrap(api.GET('/api/subscriptions')));

	let editorOpen = $state(false);
	let editingId = $state<string>();
	let form = $state(emptyForm());
	let saving = $state(false);
	let refreshing = $state<Record<string, boolean>>({});

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
		editorOpen = true;
	}

	async function save() {
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
		const saved = await attempt(
			() =>
				id
					? unwrap(api.PUT('/api/subscriptions/{id}', { params: { path: { id } }, body }))
					: unwrap(api.POST('/api/subscriptions', { body })),
			'Подписка сохранена'
		);
		saving = false;
		if (!saved) return;
		editorOpen = false;
		await subscriptions.reload();
		if (saved.refreshJobId) {
			await follow(
				saved.id,
				api.GET('/api/jobs/{id}', { params: { path: { id: saved.refreshJobId } } })
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
		if (
			!confirm(
				`Удалить подписку «${item.displayName}» вместе с её серверами (${item.serverCount})?`
			)
		)
			return;
		const done = await attempt(
			() => unwrap(api.DELETE('/api/subscriptions/{id}', { params: { path: { id: item.id } } })),
			'Подписка удалена'
		);
		if (done !== undefined) void subscriptions.reload();
	}

	function traffic(item: Subscription): string | undefined {
		const provider = item.provider;
		if (!provider) return undefined;
		const used = (toNumber(provider.uploadBytes) ?? 0) + (toNumber(provider.downloadBytes) ?? 0);
		const total = toNumber(provider.totalBytes);
		if (!used && !total) return undefined;
		return total ? `${formatBytes(used)} из ${formatBytes(total)}` : formatBytes(used);
	}
</script>

<div class="toolbar">
	<h1>Подписки</h1>
	<button onclick={openCreate}>Добавить</button>
</div>

<Loadable resource={subscriptions}>
	{#snippet children(list)}
		{#if list.length === 0}
			<p class="card empty">Подписок нет</p>
		{:else}
			<div class="grid">
				{#each list as item (item.id)}
					{@const last = item.lastRefresh}
					{@const status = label(refreshStatuses, last?.status)}
					<section class="card">
						<header>
							<h2>{item.displayName}</h2>
							{#if last}<Badge tone={status.tone}>{status.label}</Badge>{/if}
						</header>
						<dl class="props">
							<dt>Адрес</dt>
							<dd>{hostOf(item.url)}</dd>
							<dt>Серверов</dt>
							<dd>{item.serverCount}</dd>
							{#if traffic(item)}
								<dt>Трафик</dt>
								<dd>{traffic(item)}</dd>
							{/if}
							{#if item.provider?.expireAt}
								<dt>Действует до</dt>
								<dd>{formatDateTime(item.provider.expireAt)}</dd>
							{/if}
							<dt>Обновлена</dt>
							<dd>{formatDateTime(last?.at)}</dd>
							<dt>Следующее</dt>
							<dd>
								{item.autoUpdate
									? `${formatDateTime(item.nextRefreshAt)} (каждые ${item.effectiveIntervalHours} ч)`
									: 'вручную'}
							</dd>
						</dl>
						{#if last?.error}<p class="error">{last.error}</p>{/if}
						{#if last && (toNumber(last.rejected) ?? 0) > 0}
							<p class="muted">
								Узлов: {last.received}, разобрано {last.accepted}, отклонено {last.rejected}
							</p>
						{/if}
						{#if item.provider?.announce}
							<p class="muted">{item.provider.announce}</p>
						{/if}
						<div class="actions">
							<UpdateButton
								small
								label="Обновить"
								busy={refreshing[item.id]}
								onrun={(via) => refresh(item.id, via)}
							/>
							<button class="secondary small" onclick={() => openEdit(item)}>Изменить</button>
							<button class="danger small" onclick={() => remove(item)}>Удалить</button>
						</div>
					</section>
				{/each}
			</div>
		{/if}
	{/snippet}
</Loadable>

<Dialog bind:open={editorOpen} title={editingId ? 'Подписка' : 'Новая подписка'}>
	<div class="form">
		<label>
			Адрес подписки
			<input bind:value={form.url} placeholder="https://…" type="url" />
			<span class="hint">Содержит токен доступа — не показывайте его посторонним.</span>
		</label>
		<label>
			Имя
			<input bind:value={form.name} placeholder="по умолчанию — имя провайдера" />
		</label>
		<label class="check"
			><input type="checkbox" bind:checked={form.autoUpdate} /> Обновлять по расписанию</label
		>
		<label>
			Интервал обновления
			<select bind:value={form.interval}>
				<option value="">как у провайдера или по умолчанию</option>
				{#each intervalOptions(form.interval) as hours (hours)}
					<option value={hours.toString()}>{hoursLabel(hours)}</option>
				{/each}
			</select>
		</label>
		<label>
			User-Agent
			<input bind:value={form.userAgent} placeholder="FlowGate/<версия>" />
			<span class="hint">Некоторые провайдеры отдают Xray-JSON только клиентам вроде Happ.</span>
		</label>
		<label class="check"
			><input type="checkbox" bind:checked={form.sendHwid} /> Отправлять x-hwid</label
		>
		<label class="check">
			<input type="checkbox" bind:checked={form.sendDeviceInfo} /> Сообщать модель и версию роутера
		</label>
	</div>
	{#snippet actions()}
		<button class="secondary" onclick={() => (editorOpen = false)}>Отмена</button>
		<button disabled={saving || !form.url.trim()} onclick={save}>Сохранить</button>
	{/snippet}
</Dialog>

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

	.actions {
		margin-top: 12px;
	}

	section p {
		margin: 8px 0 0;
	}
</style>
