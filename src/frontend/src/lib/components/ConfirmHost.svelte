<script lang="ts">
	import { confirmState, settle } from '#lib/confirm.svelte.js';
	import Dialog from './Dialog.svelte';

	// Закрытие окна по Esc или подложке — это отказ.
	$effect(() => {
		if (!confirmState.open && confirmState.resolve) settle(false);
	});
</script>

<Dialog
	bind:open={confirmState.open}
	title={confirmState.title}
	size="sm"
	onsubmit={() => settle(true)}
>
	{#if confirmState.message}<p class="message">{confirmState.message}</p>{/if}
	{#snippet actions()}
		<button class="btn" onclick={() => settle(false)}>Отмена</button>
		<!-- svelte-ignore a11y_autofocus -->
		<button
			class="btn {confirmState.danger ? 'danger' : 'primary'}"
			autofocus
			onclick={() => settle(true)}>{confirmState.action}</button
		>
	{/snippet}
</Dialog>

<style>
	.message {
		color: var(--ink-2);
	}
</style>
