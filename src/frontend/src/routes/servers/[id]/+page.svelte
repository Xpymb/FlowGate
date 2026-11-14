<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import { ask } from '#lib/confirm.svelte.js';
	import { formatDateTime } from '#lib/format.js';
	import { attempt, attemptOk } from '#lib/toast.svelte.js';
	import { guardUnsaved } from '#lib/unsaved.svelte.js';
	import type { components } from '#lib/api/schema.js';
	import Badge from '#lib/components/Badge.svelte';
	import Copyable from '#lib/components/Copyable.svelte';
	import EmptyState from '#lib/components/EmptyState.svelte';
	import Field from '#lib/components/Field.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import JsonEditor from '#lib/components/JsonEditor.svelte';
	import Menu from '#lib/components/Menu.svelte';
	import PageHeader from '#lib/components/PageHeader.svelte';
	import SaveBar from '#lib/components/SaveBar.svelte';

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
	let saved = $state({ name: '', text: '' });
	let link = $state('');
	let warnings = $state<string[]>([]);
	let validation = $state<Validation>();
	let parsing = $state(false);
	let validating = $state(false);
	let saving = $state(false);

	const dirty = $derived(editable && (name !== saved.name || text !== saved.text));
	const invalid = $derived(editable && !name.trim() ? 'Укажите имя сервера' : undefined);

	guardUnsaved(() => dirty && !saving);

	$effect(() => {
		void load(id);
	});

	async function load(current: string) {
		loading = true;
		loadError = undefined;
		validation = undefined;
		warnings = [];
		link = '';
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
			saved = { name, text };
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
		if (!link.trim()) return;
		parsing = true;
		const draft = await attempt(() =>
			unwrap(api.POST('/api/servers/parse', { body: { text: link } }))
		);
		parsing = false;
		if (!draft) return;
		text = JSON.stringify(draft.outbound, null, 2);
		if (!name.trim()) name = draft.name;
		warnings = draft.warnings;
		validation = undefined;
		link = '';
	}

	async function validate() {
		const outbound = parseOutbound();
		if (!outbound) return;
		validating = true;
		const result = await attempt(() =>
			unwrap(api.POST('/api/servers/validate', { body: { outbound } }))
		);
		validating = false;
		if (result) validation = result;
	}

	async function save() {
		const outbound = parseOutbound();
		if (!outbound) return;
		saving = true;
		const body = { name: name.trim(), outbound };
		const result = await attempt(
			() =>
				isNew
					? unwrap(api.POST('/api/servers', { body }))
					: unwrap(api.PUT('/api/servers/{id}', { params: { path: { id } }, body })),
			isNew ? 'Сервер добавлен' : 'Изменения сохранены'
		);
		if (!result) {
			saving = false;
			return;
		}
		server = result.server;
		name = result.server.name;
		text = JSON.stringify(result.outbound, null, 2);
		saved = { name, text };
		saving = false;
		if (isNew) await goto(resolve('/servers/[id]', { id: result.server.id }), { replace: true });
	}

	async function remove() {
		if (!server) return;
		const ok = await ask({
			title: `Удалить сервер «${server.name}»?`,
			message: 'Соединения, использующие этот сервер, перестанут работать.',
			action: 'Удалить',
			danger: true
		});
		if (!ok) return;
		const done = await attemptOk(
			() => unwrap(api.DELETE('/api/servers/{id}', { params: { path: { id } } })),
			'Сервер удалён'
		);
		if (done) {
			saved = { name, text };
			await goto(resolve('/servers'));
		}
	}

	function resetChanges() {
		name = saved.name;
		text = saved.text;
		validation = undefined;
		warnings = [];
	}
</script>

<PageHeader
	title={isNew ? 'Новый сервер' : (server?.name ?? 'Сервер')}
	back={{ href: resolve('/servers'), label: 'Серверы' }}
>
	{#snippet meta()}
		{#if server}
			{#if server.source === 'Subscription'}<Badge tone="accent">из подписки</Badge>
			{:else}<Badge>вручную</Badge>{/if}
		{/if}
	{/snippet}
	{#snippet actions()}
		{#if !loading && !loadError}
			<button class="btn" aria-busy={validating} onclick={validate}>
				<Icon name="check" size={16} />Проверить конфигурацию
			</button>
			{#if server && editable}
				<Menu
					small={false}
					buttonClass=""
					items={[{ label: 'Удалить сервер', icon: 'trash', danger: true, onclick: remove }]}
				/>
			{/if}
		{/if}
	{/snippet}
</PageHeader>

{#if loading}
	<div class="panel loading" aria-busy="true">Загрузка…</div>
{:else if loadError}
	<EmptyState icon="alert" title="Не удалось загрузить сервер" text={errorMessage(loadError)}>
		<button class="btn" onclick={() => load(id)}>Повторить</button>
		<a class="btn" href={resolve('/servers')}>К списку серверов</a>
	</EmptyState>
{:else}
	<div class="layout">
		<div class="main stack">
			{#if !editable}
				<div class="note" role="note">
					<Icon name="lock" size={16} />
					<span>
						Сервер получен из подписки и доступен только для чтения. Изменения будут перезаписаны
						при следующем обновлении.
					</span>
				</div>
			{/if}

			{#if editable}
				<section class="panel panel-body form">
					<Field label="Имя" hint="Отображается в списках и при выборе сервера.">
						<input bind:value={name} maxlength="128" placeholder="Нидерланды" />
					</Field>
					<Field
						label="Импорт из ссылки"
						hint="vless://, vmess://, trojan://, ss:// или Xray-JSON. Заменяет текущий outbound."
					>
						<div class="link">
							<input
								bind:value={link}
								placeholder="vless://…"
								spellcheck="false"
								onkeydown={(event) => event.key === 'Enter' && fromLink()}
							/>
							<button class="btn" disabled={!link.trim()} aria-busy={parsing} onclick={fromLink}>
								Применить
							</button>
						</div>
					</Field>
				</section>
			{/if}

			<section class="panel">
				<header class="panel-head">
					<h2>Outbound Xray</h2>
					{#if validation}
						{#if validation.valid}
							<Badge tone="ok" dot>
								{validation.checkedByXray ? 'Xray: ошибок нет' : 'JSON в порядке'}
							</Badge>
						{:else}
							<Badge tone="danger" dot>есть ошибки</Badge>
						{/if}
					{/if}
				</header>
				<div class="panel-body editor-body">
					<p class="muted small">
						Поле <code>tag</code> не указывается: FlowGate назначает его автоматически.
					</p>
					<JsonEditor bind:value={text} readonly={!editable} label="Outbound Xray" />
					{#if validation && !validation.valid}
						<ul class="issues error-text">
							{#each validation.errors as error (error)}<li>{error}</li>{/each}
						</ul>
					{/if}
					{#if validation?.valid && !validation.checkedByXray}
						<p class="muted small">
							Xray не установлен: выполнена только проверка структуры, без проверки в Xray.
						</p>
					{/if}
					{#if warnings.length > 0}
						<ul class="issues muted">
							{#each warnings as warning (warning)}<li>{warning}</li>{/each}
						</ul>
					{/if}
				</div>
			</section>
		</div>

		{#if server}
			<aside class="panel panel-body side">
				<h2>Сведения</h2>
				<dl class="props">
					<dt>Протокол</dt>
					<dd class="mono">{server.protocol}</dd>
					<dt>Транспорт</dt>
					<dd>{server.network} · {server.security}</dd>
					<dt>Адрес</dt>
					<dd><Copyable value={`${server.address}${server.port ? `:${server.port}` : ''}`} /></dd>
					{#if server.serverName}
						<dt>SNI</dt>
						<dd><Copyable value={server.serverName} /></dd>
					{/if}
					<dt>Изменён</dt>
					<dd>{formatDateTime(server.updatedAt)}</dd>
				</dl>
			</aside>
		{/if}
	</div>

	{#if editable}
		<SaveBar
			dirty={dirty || isNew}
			{saving}
			{invalid}
			saveLabel={isNew ? 'Добавить сервер' : 'Сохранить'}
			onsave={save}
			onreset={isNew ? undefined : resetChanges}
		/>
	{/if}
{/if}

<style>
	.loading {
		padding: 24px;
		color: var(--muted);
	}

	.layout {
		display: grid;
		gap: 20px;
		align-items: start;
	}

	@media (min-width: 1100px) {
		.layout {
			grid-template-columns: minmax(0, 1fr) 300px;
		}
	}

	.side {
		display: grid;
		gap: 16px;
	}

	.side dl.props {
		grid-template-columns: max-content 1fr;
		gap: 10px 16px;
	}

	.note {
		display: flex;
		align-items: center;
		gap: 10px;
		padding: 12px 16px;
		border-radius: var(--r-md);
		background: var(--info-soft);
		color: var(--ink-2);
	}

	.link {
		display: flex;
		gap: 8px;
	}

	.editor-body {
		display: grid;
		gap: 12px;
	}

	.issues {
		display: grid;
		gap: 4px;
		margin: 0;
		padding-left: 18px;
		font-size: 13px;
	}
</style>
