<script lang="ts">
	import { kindTitle, setup } from '#lib/setup/setup.svelte.js';
	import Icon from '../Icon.svelte';

	const run = $derived(setup.run);
	const draft = $derived(setup.draft);
	/** Что пойдёт через каждое соединение. */
	const plan = $derived(
		draft.connections.map((connection, index) => ({
			uid: connection.uid,
			title: `${index + 1}. ${kindTitle(connection.kind)} через «${setup.serverName(connection)}»`,
			services: draft.selected
				.filter((item) => item.connectionUid === connection.uid)
				.map((item) => item.title)
		}))
	);
	const warnings = $derived([...new Set(run.warnings)]);
	const visibleSteps = $derived(
		run.steps.filter((step) => !(step.id === 'proxy' && !setup.draft.useUpdateProxy))
	);
</script>

{#if run.phase === 'idle'}
	<ul class="plan">
		{#each plan as item (item.uid)}
			<li>
				<span class="plan-title">{item.title}</span>
				<span class:muted={item.services.length === 0}>
					{item.services.length > 0
						? item.services.join(', ')
						: 'Сервисов нет: соединение создастся без маршрутов'}
				</span>
			</li>
		{/each}
	</ul>
	<dl class="props summary">
		<dt>Обновления</dt>
		<dd>
			{draft.useUpdateProxy && draft.connections[0]
				? `Через «${setup.serverName(draft.connections[0])}»`
				: 'Напрямую'}
		</dd>
	</dl>
	<p class="muted small note">
		FlowGate установит Xray, добавит серверы, создаст интерфейсы и DNS-маршруты в Keenetic. Это
		займёт около минуты.
	</p>
{:else}
	{#if run.phase === 'done'}
		<div class="outcome ok">
			<span class="badge"><Icon name="check" size={20} /></span>
			<div>
				<h4>Настроено</h4>
				<p class="muted">
					Выбранные сервисы идут через VPN. Откройте один из них на устройстве в домашней сети,
					чтобы проверить.
				</p>
			</div>
		</div>
	{/if}

	<ol class="steps" role="status" aria-live="polite">
		{#each visibleSteps as step (step.id)}
			<li class={step.status}>
				<span class="mark">
					{#if step.status === 'done' || step.status === 'skipped'}
						<Icon name="check" size={14} />
					{:else if step.status === 'failed'}
						<Icon name="alert" size={14} />
					{:else if step.status === 'running'}
						<span class="spinner"></span>
					{/if}
				</span>
				<span class="body">
					<span class="title">{step.title}</span>
					{#if step.error}
						<span class="error-text small">{step.error}</span>
					{:else if step.detail}
						<span class="detail small">{step.detail}</span>
					{/if}
				</span>
			</li>
		{/each}
	</ol>

	{#if warnings.length > 0}
		<ul class="warnings small">
			{#each warnings as warning (warning)}
				<li><Icon name="info" size={14} />{warning}</li>
			{/each}
		</ul>
	{/if}
{/if}

<style>
	.plan {
		display: grid;
		gap: 12px;
		margin: 0 0 20px;
		padding: 0;
		list-style: none;
	}

	.plan li {
		display: grid;
		gap: 2px;
		padding: 12px 14px;
		border: 1px solid var(--line);
		border-radius: var(--r-sm);
		overflow-wrap: anywhere;
	}

	.plan-title {
		font-weight: 500;
	}

	.summary {
		grid-template-columns: max-content minmax(0, 1fr);
		gap: 10px 24px;
	}

	.summary dd {
		overflow-wrap: anywhere;
	}

	.note {
		margin-top: 20px;
	}

	.outcome {
		display: flex;
		align-items: flex-start;
		gap: 14px;
		margin-bottom: 20px;
		padding: 16px;
		border-radius: var(--r-md);
		background: var(--ok-soft);
	}

	.outcome .badge {
		display: grid;
		flex: none;
		place-items: center;
		width: 36px;
		height: 36px;
		border-radius: 10px;
		background: var(--surface);
		color: var(--ok);
	}

	.outcome h4 {
		margin: 0 0 2px;
		font-size: 15px;
	}

	.steps {
		display: grid;
		gap: 2px;
		margin: 0;
		padding: 0;
		list-style: none;
	}

	.steps li {
		display: grid;
		grid-template-columns: 24px minmax(0, 1fr);
		gap: 10px;
		padding: 8px 0;
	}

	.mark {
		display: grid;
		place-items: center;
		width: 22px;
		height: 22px;
		margin-top: 1px;
		border: 1.5px solid var(--line-strong);
		border-radius: 999px;
		color: var(--faint);
	}

	.done .mark,
	.skipped .mark {
		border-color: transparent;
		background: var(--ok-soft);
		color: var(--ok);
	}

	.failed .mark {
		border-color: transparent;
		background: var(--danger-soft);
		color: var(--danger);
	}

	.running .mark {
		border-color: var(--accent);
	}

	.spinner {
		width: 10px;
		height: 10px;
		border: 2px solid var(--accent);
		border-right-color: transparent;
		border-radius: 999px;
		animation: spin 0.8s linear infinite;
	}

	.body {
		display: grid;
		gap: 2px;
		min-width: 0;
	}

	.title {
		font-weight: 500;
	}

	.pending .title {
		color: var(--muted);
	}

	.detail {
		color: var(--muted);
		overflow-wrap: anywhere;
	}

	.warnings {
		display: grid;
		gap: 6px;
		margin: 16px 0 0;
		padding: 12px 14px;
		border-radius: var(--r-sm);
		background: var(--warn-soft);
		color: var(--warn);
		list-style: none;
	}

	.warnings li {
		display: flex;
		gap: 8px;
	}

	.warnings li :global(.icon) {
		flex: none;
		margin-top: 2px;
	}

	@keyframes spin {
		to {
			transform: rotate(360deg);
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.spinner {
			animation-duration: 2.4s;
		}
	}
</style>
