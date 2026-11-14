<script lang="ts">
	import { api } from '#lib/api/client.js';
	import { errorMessage, unwrap } from '#lib/api/errors.js';
	import { connectionKindOptions } from '#lib/connections.js';
	import { maxConnections, setup, type ServerMode } from '#lib/setup/setup.svelte.js';
	import Field from '../Field.svelte';
	import Icon from '../Icon.svelte';

	const draft = $derived(setup.draft);
	let checking = $state(false);
	let error = $state('');

	const modes = $derived<{ value: ServerMode; label: string }[]>([
		{ value: 'subscription', label: 'Подписка' },
		{ value: 'links', label: 'Ссылки' },
		...(setup.existingServers.length > 0
			? [{ value: 'existing' as const, label: 'Имеющиеся серверы' }]
			: [])
	]);
	const options = $derived(setup.serverOptions);
	const preview = $derived(setup.preview);

	function setMode(mode: ServerMode) {
		draft.serverMode = mode;
		error = '';
		setup.fillServers();
	}

	async function check() {
		const input = setup.input;
		if (!input || checking) return;
		checking = true;
		error = '';
		try {
			const result =
				draft.serverMode === 'subscription'
					? await unwrap(
							api.POST('/api/subscriptions/preview', {
								body: { url: input, autoUpdate: true, sendHwid: false, sendDeviceInfo: false }
							})
						)
					: await unwrap(api.POST('/api/servers/preview', { body: { text: input } }));
			draft.preview = result;
			draft.previewInput = `${draft.serverMode}:${input}`;
			setup.fillServers();
			if (result.servers.length === 0)
				error =
					result.rejected.length > 0
						? 'Ни один сервер не удалось разобрать. Причины — ниже.'
						: 'Серверов не найдено. Проверьте, что это ссылка на подписку, а не на страницу провайдера.';
		} catch (failure) {
			error = errorMessage(failure);
		} finally {
			checking = false;
		}
	}

	function onkeydown(event: KeyboardEvent) {
		if (event.key === 'Enter' && draft.serverMode === 'subscription') {
			event.preventDefault();
			void check();
		}
	}
</script>

<div class="form">
	<Field label="Откуда взять серверы" group>
		<div class="segmented">
			{#each modes as mode (mode.value)}
				<label class:selected={draft.serverMode === mode.value}>
					<input
						type="radio"
						name="server-mode"
						value={mode.value}
						checked={draft.serverMode === mode.value}
						onchange={() => setMode(mode.value)}
					/>
					{mode.label}
				</label>
			{/each}
		</div>
	</Field>

	{#if draft.serverMode === 'subscription'}
		<Field
			label="Ссылка на подписку"
			hint="Её выдаёт VPN-провайдер: в личном кабинете или в боте. Начинается с https://"
			error={error || undefined}
		>
			<div class="input-row">
				<input
					bind:value={draft.subscriptionUrl}
					type="url"
					inputmode="url"
					autocomplete="off"
					spellcheck="false"
					placeholder="https://"
					{onkeydown}
				/>
				<button class="btn" aria-busy={checking} disabled={!setup.input} onclick={check}>
					Проверить
				</button>
			</div>
		</Field>
	{:else if draft.serverMode === 'links'}
		<Field
			label="Ссылки на серверы"
			hint="По одной на строку: vless://, vmess://, trojan://, ss://, hysteria2://. Подойдёт и base64 или Xray-JSON."
			error={error || undefined}
		>
			<textarea
				bind:value={draft.linksText}
				rows="4"
				spellcheck="false"
				autocomplete="off"
				placeholder="vless://…"></textarea>
		</Field>
		<button class="btn check-links" aria-busy={checking} disabled={!setup.input} onclick={check}>
			Проверить ссылки
		</button>
	{/if}

	{#if preview && preview.rejected.length > 0}
		<details class="rejected">
			<summary>Не удалось разобрать: {preview.rejected.length}</summary>
			<ul>
				{#each preview.rejected as rejection (rejection.index)}
					<li><span class="muted">№{rejection.index}</span> {rejection.reason}</li>
				{/each}
			</ul>
		</details>
	{/if}

	{#if options.length > 0}
		<section class="connections" aria-labelledby="connections-title">
			<div class="section-head">
				<h4 id="connections-title">Соединения</h4>
				<span class="muted small">
					{preview?.title ? `Подписка «${preview.title}»: ` : ''}серверов {options.length}
				</span>
			</div>
			<p class="muted small">
				Каждое соединение — отдельный интерфейс в Keenetic со своим сервером. На следующем шаге вы
				выберете, какие сервисы через какое соединение пускать.
			</p>

			<ol class="rows">
				{#each draft.connections as connection, index (connection.uid)}
					<li class="row-card">
						<span class="index" aria-hidden="true">{index + 1}</span>
						<label class="server">
							<span class="visually-hidden">Сервер соединения {index + 1}</span>
							<select bind:value={connection.serverRef}>
								{#each options as option (option.ref)}
									<option value={option.ref}>{option.name} — {option.endpoint}</option>
								{/each}
							</select>
						</label>
						<div class="segmented kind" role="radiogroup" aria-label="Тип соединения {index + 1}">
							{#each connectionKindOptions as kind (kind.value)}
								<label class:selected={connection.kind === kind.value} title={kind.text}>
									<input
										type="radio"
										name="kind-{connection.uid}"
										value={kind.value}
										bind:group={connection.kind}
									/>
									{kind.title}
								</label>
							{/each}
						</div>
						{#if draft.connections.length > 1}
							<button
								class="btn ghost icon sm"
								aria-label="Убрать соединение {index + 1}"
								onclick={() => setup.removeConnection(connection.uid)}
							>
								<Icon name="close" size={16} />
							</button>
						{/if}
					</li>
				{/each}
			</ol>

			{#if setup.duplicateConnections}
				<p class="error-text small">
					Два соединения с одним сервером и типом дублируют друг друга: смените сервер или тип.
				</p>
			{/if}
			{#if draft.connections.length < maxConnections}
				<button class="btn sm add" onclick={() => setup.addConnection()}>
					<Icon name="plus" size={15} />Добавить соединение
				</button>
			{/if}
			<p class="muted small">
				WireGuard пропускает TCP и UDP (QUIC, звонки). SOCKS5 и HTTP — только TCP.
			</p>
		</section>

		<label class="check">
			<input type="checkbox" bind:checked={draft.useUpdateProxy} />
			<span>
				Загружать обновления через сервер первого соединения
				<span class="hint">
					Xray, списки доменов и подписка будут скачиваться через VPN, даже если GitHub недоступен
					напрямую.
				</span>
			</span>
		</label>
	{/if}
</div>

<style>
	.input-row {
		display: flex;
		gap: 8px;
	}

	.input-row input {
		flex: 1;
		min-width: 0;
	}

	.check-links {
		justify-self: start;
		margin-top: -8px;
	}

	.rejected {
		margin-top: -8px;
		font-size: 13px;
	}

	.rejected summary {
		color: var(--muted);
		cursor: pointer;
	}

	.rejected ul {
		margin: 8px 0 0;
		padding-left: 18px;
	}

	.connections {
		display: grid;
		gap: 10px;
		padding-top: 18px;
		border-top: 1px solid var(--line);
	}

	.section-head {
		display: flex;
		align-items: baseline;
		justify-content: space-between;
		gap: 12px;
	}

	.section-head h4 {
		margin: 0;
		font-size: 14px;
	}

	.rows {
		display: grid;
		gap: 8px;
		margin: 0;
		padding: 0;
		list-style: none;
	}

	.row-card {
		display: grid;
		grid-template-columns: 24px minmax(0, 1fr) auto 28px;
		align-items: center;
		gap: 10px;
		padding: 8px 10px;
		border: 1px solid var(--line-strong);
		border-radius: var(--r-sm);
	}

	.index {
		display: grid;
		place-items: center;
		width: 24px;
		height: 24px;
		border-radius: 999px;
		background: var(--accent-soft);
		color: var(--accent-text);
		font-size: 12.5px;
		font-weight: 600;
	}

	.server select {
		width: 100%;
	}

	.add {
		justify-self: start;
	}

	@media (max-width: 720px) {
		.row-card {
			grid-template-columns: 24px minmax(0, 1fr) 28px;
		}

		.kind {
			grid-column: 2 / 4;
			grid-row: 2;
		}
	}
</style>
