<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import Badge from '#lib/components/Badge.svelte';
	import JsonEditor from '#lib/components/JsonEditor.svelte';
	import { formatDateTime } from '#lib/format.js';
	import { attempt } from '#lib/toast.svelte.js';
	import type { components } from '#lib/api/schema.js';

	type Server = components['schemas']['ServerDto'];
	type Validation = components['schemas']['ServerValidationDto'];

	const template = {
		protocol: 'vless',
		settings: { address: 'example.com', port: 443, id: '', encryption: 'none' },
		streamSettings: { network: 'tcp', security: 'reality' }
	};

	const id = $derived(page.params.id ?? 'new');
	const isNew = $derived(id === 'new');

	let loading = $state(true);
	let loadError = $state<unknown>();
	let server = $state<Server>();
	let editable = $state(true);
	let name = $state('');
	let text = $state('');
	let link = $state('');
	let warnings = $state<string[]>([]);
	let validation = $state<Validation>();
	let busy = $state(false);

	$effect(() => {
		void load(id);
	});

	async function load(current: string) {
		loading = true;
		loadError = undefined;
		validation = undefined;
		warnings = [];
		try {
			if (current === 'new') {
				server = undefined;
				editable = true;
				name = '';
				text = JSON.stringify(template, null, 2);
			} else {
				const details = await unwrap(
					api.GET('/api/servers/{id}', { params: { path: { id: current } } })
				);
				server = details.server;
				editable = details.editable;
				name = details.server.name;
				text = JSON.stringify(details.outbound, null, 2);
			}
		} catch (error) {
			loadError = error;
		} finally {
			loading = false;
		}
	}

	function parseOutbound(): Record<string, unknown> | undefined {
		try {
			const value: unknown = JSON.parse(text);
			if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
				return value as Record<string, unknown>;
			}
			validation = { valid: false, checkedByXray: false, errors: ['Нужен JSON-объект outbound'] };
		} catch (error) {
			validation = { valid: false, checkedByXray: false, errors: [`JSON: ${errorMessage(error)}`] };
		}
		return undefined;
	}

	async function fromLink() {
		busy = true;
		const draft = await attempt(() =>
			unwrap(api.POST('/api/servers/parse', { body: { text: link } }))
		);
		busy = false;
		if (!draft) return;
		text = JSON.stringify(draft.outbound, null, 2);
		if (!name.trim()) name = draft.name;
		warnings = draft.warnings;
		validation = undefined;
	}

	async function validate() {
		const outbound = parseOutbound();
		if (!outbound) return;
		busy = true;
		const result = await attempt(() =>
			unwrap(api.POST('/api/servers/validate', { body: { outbound } }))
		);
		busy = false;
		if (result) validation = result;
	}

	async function save() {
		const outbound = parseOutbound();
		if (!outbound) return;
		busy = true;
		const body = { name: name.trim(), outbound };
		const saved = await attempt(
			() =>
				isNew
					? unwrap(api.POST('/api/servers', { body }))
					: unwrap(api.PUT('/api/servers/{id}', { params: { path: { id } }, body })),
			'Сервер сохранён'
		);
		busy = false;
		if (!saved) return;
		if (isNew) {
			await goto(resolve('/servers/[id]', { id: saved.server.id }), { replaceState: true });
		} else {
			server = saved.server;
			text = JSON.stringify(saved.outbound, null, 2);
		}
	}

	async function remove() {
		if (!server || !confirm(`Удалить сервер «${server.name}»?`)) return;
		const done = await attempt(
			() => unwrap(api.DELETE('/api/servers/{id}', { params: { path: { id } } })),
			'Сервер удалён'
		);
		if (done !== undefined) await goto(resolve('/servers'));
	}
</script>

<div class="toolbar">
	<h1>{isNew ? 'Новый сервер' : (server?.name ?? 'Сервер')}</h1>
	<a href={resolve('/servers')}>← Все серверы</a>
</div>

{#if loading}
	<p class="muted">Загрузка…</p>
{:else if loadError}
	<p class="error">{errorMessage(loadError)}</p>
{:else}
	<div class="stack">
		{#if server}
			<section class="card">
				<dl class="props">
					<dt>Протокол</dt>
					<dd>{server.protocol}, {server.network} / {server.security}</dd>
					<dt>Адрес</dt>
					<dd>{server.address}{server.port ? `:${server.port}` : ''}</dd>
					{#if server.serverName}
						<dt>SNI</dt>
						<dd>{server.serverName}</dd>
					{/if}
					<dt>Источник</dt>
					<dd>{server.source === 'Subscription' ? 'подписка' : 'вручную'}</dd>
					<dt>Изменён</dt>
					<dd>{formatDateTime(server.updatedAt)}</dd>
				</dl>
				{#if !editable}
					<p class="muted note">
						Сервер из подписки: правки перезапишет следующее обновление, поэтому он только для
						чтения.
					</p>
				{/if}
			</section>
		{/if}

		<section class="card form">
			{#if editable}
				<label>
					Имя
					<input bind:value={name} maxlength="128" />
				</label>
				<div class="link">
					<label>
						Заполнить из ссылки
						<input bind:value={link} placeholder="vless://… или Xray-JSON" />
					</label>
					<button class="secondary" disabled={busy || !link.trim()} onclick={fromLink}
						>Разобрать</button
					>
				</div>
			{/if}
			<div>
				<div class="label-row">
					<span>Outbound Xray (без <code>tag</code>)</span>
					{#if validation}
						{#if validation.valid}
							<Badge tone="ok"
								>{validation.checkedByXray ? 'Xray: ошибок нет' : 'JSON в порядке'}</Badge
							>
						{:else}
							<Badge tone="danger">есть ошибки</Badge>
						{/if}
					{/if}
				</div>
				<JsonEditor bind:value={text} readonly={!editable} />
			</div>
			{#if warnings.length > 0}
				<ul class="plain muted">
					{#each warnings as warning (warning)}<li>{warning}</li>{/each}
				</ul>
			{/if}
			{#if validation && !validation.valid}
				<ul class="plain error">
					{#each validation.errors as error (error)}<li>{error}</li>{/each}
				</ul>
			{/if}
			{#if validation?.valid && !validation.checkedByXray}
				<p class="hint">Xray не установлен: проверены только поля, сам Xray конфиг не проверял.</p>
			{/if}
			<div class="actions">
				<button class="secondary" disabled={busy} onclick={validate}>Проверить</button>
				{#if editable}
					<button disabled={busy || !name.trim()} onclick={save}>Сохранить</button>
					{#if !isNew}
						<button class="danger" disabled={busy} onclick={remove}>Удалить</button>
					{/if}
				{/if}
			</div>
		</section>
	</div>
{/if}

<style>
	.note {
		margin: 12px 0 0;
	}

	.link {
		display: grid;
		grid-template-columns: 1fr auto;
		gap: 8px;
		align-items: end;
	}

	.label-row {
		display: flex;
		align-items: center;
		gap: 8px;
		margin-bottom: 4px;
	}
</style>
