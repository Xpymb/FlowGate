<script lang="ts">
	import { dismiss, toasts } from '#lib/toast.svelte.js';
</script>

<div class="toasts" aria-live="polite">
	{#each toasts as toast (toast.id)}
		<div class="toast {toast.tone}" role={toast.tone === 'error' ? 'alert' : 'status'}>
			<div class="text">{toast.text}</div>
			{#if toast.detail}<pre>{toast.detail}</pre>{/if}
			<button class="close" aria-label="Закрыть" onclick={() => dismiss(toast.id)}>×</button>
		</div>
	{/each}
</div>

<style>
	.toasts {
		position: fixed;
		right: 16px;
		bottom: 16px;
		display: flex;
		flex-direction: column;
		gap: 8px;
		width: min(420px, calc(100vw - 32px));
		z-index: 20;
	}

	.toast {
		position: relative;
		padding: 10px 36px 10px 12px;
		border-radius: var(--radius);
		background: var(--surface);
		border: 1px solid var(--border);
		border-left: 4px solid var(--accent);
		box-shadow: 0 4px 16px rgb(0 0 0 / 0.15);
	}

	.toast.ok {
		border-left-color: var(--ok);
	}

	.toast.error {
		border-left-color: var(--danger);
	}

	pre {
		margin: 6px 0 0;
		white-space: pre-wrap;
		color: var(--muted);
	}

	.close {
		position: absolute;
		top: 4px;
		right: 4px;
		padding: 0 8px;
		border: none;
		background: transparent;
		color: var(--muted);
		font-size: 1.2rem;
	}
</style>
