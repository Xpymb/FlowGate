<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		open: boolean;
		title: string;
		wide?: boolean;
		children: Snippet;
		actions?: Snippet;
	}

	let { open = $bindable(), title, wide = false, children, actions }: Props = $props();
	let dialog: HTMLDialogElement;

	$effect(() => {
		if (open && !dialog.open) dialog.showModal();
		if (!open && dialog.open) dialog.close();
	});
</script>

<dialog bind:this={dialog} class:wide onclose={() => (open = false)}>
	<header>
		<h2>{title}</h2>
		<button class="close" aria-label="Закрыть" onclick={() => (open = false)}>×</button>
	</header>
	{#if open}
		<div class="body">{@render children()}</div>
		{#if actions}
			<footer>{@render actions()}</footer>
		{/if}
	{/if}
</dialog>

<style>
	dialog {
		width: min(560px, calc(100vw - 32px));
		max-height: calc(100vh - 48px);
		padding: 0;
		border: 1px solid var(--border);
		border-radius: var(--radius);
		background: var(--surface);
		color: var(--text);
	}

	dialog.wide {
		width: min(900px, calc(100vw - 32px));
	}

	dialog::backdrop {
		background: rgb(0 0 0 / 0.45);
	}

	header {
		display: flex;
		align-items: center;
		padding: 12px 16px;
		border-bottom: 1px solid var(--border);
	}

	h2 {
		margin: 0;
	}

	.body {
		padding: 16px;
	}

	footer {
		display: flex;
		justify-content: flex-end;
		gap: 8px;
		padding: 12px 16px;
		border-top: 1px solid var(--border);
	}

	.close {
		margin-left: auto;
		padding: 0 8px;
		border: none;
		background: transparent;
		color: var(--muted);
		font-size: 1.3rem;
	}
</style>
