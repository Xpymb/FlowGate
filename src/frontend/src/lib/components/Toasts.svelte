<script lang="ts">
	import { dismiss, toasts } from '#lib/toast.svelte.js';
	import Icon from './Icon.svelte';

	const icons = { ok: 'check', error: 'alert', info: 'info' } as const;
</script>

<div class="toasts" aria-live="polite">
	{#each toasts as toast (toast.id)}
		<div class="toast {toast.tone}" role={toast.tone === 'error' ? 'alert' : 'status'}>
			<span class="mark"><Icon name={icons[toast.tone]} size={16} /></span>
			<div class="content">
				<div class="text">{toast.text}</div>
				{#if toast.detail}<pre>{toast.detail}</pre>{/if}
			</div>
			{#if toast.action}
				<button
					class="btn ghost sm"
					onclick={() => {
						toast.action?.run();
						dismiss(toast.id);
					}}>{toast.action.label}</button
				>
			{/if}
			<button class="btn ghost sm icon" aria-label="Закрыть" onclick={() => dismiss(toast.id)}>
				<Icon name="close" size={15} />
			</button>
		</div>
	{/each}
</div>

<style>
	.toasts {
		position: fixed;
		top: 16px;
		right: 16px;
		z-index: 60;
		display: flex;
		flex-direction: column;
		gap: 8px;
		width: min(400px, calc(100vw - 32px));
		pointer-events: none;
	}

	.toast {
		display: flex;
		align-items: flex-start;
		gap: 10px;
		padding: 10px 8px 10px 12px;
		border: 1px solid var(--line);
		border-radius: var(--r-md);
		background: var(--surface);
		box-shadow: var(--shadow-pop);
		pointer-events: auto;
		animation: enter 0.18s var(--ease);
	}

	.mark {
		display: grid;
		place-items: center;
		width: 24px;
		height: 24px;
		border-radius: 6px;
		background: var(--info-soft);
		color: var(--accent-text);
	}

	.ok .mark {
		background: var(--ok-soft);
		color: var(--ok);
	}

	.error .mark {
		background: var(--danger-soft);
		color: var(--danger);
	}

	.content {
		flex: 1;
		min-width: 0;
		padding-top: 2px;
	}

	.text {
		font-weight: 500;
	}

	pre {
		margin: 4px 0 0;
		color: var(--muted);
		font-size: 12px;
		white-space: pre-wrap;
		overflow-wrap: anywhere;
	}

	@keyframes enter {
		from {
			opacity: 0;
			transform: translateY(-6px);
		}
	}
</style>
