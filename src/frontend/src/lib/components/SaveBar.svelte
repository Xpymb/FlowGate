<script lang="ts">
	interface Props {
		dirty: boolean;
		saving?: boolean;
		/** Причина, по которой сохранить нельзя (пустое имя и т. п.). */
		invalid?: string;
		saveLabel?: string;
		onsave: () => void;
		onreset?: () => void;
	}

	let {
		dirty,
		saving = false,
		invalid,
		saveLabel = 'Сохранить',
		onsave,
		onreset
	}: Props = $props();

	function onkeydown(event: KeyboardEvent) {
		// По физической клавише: в русской раскладке это «ы».
		if (event.code !== 'KeyS' || !(event.ctrlKey || event.metaKey)) return;
		event.preventDefault();
		if (dirty && !saving && !invalid) onsave();
	}
</script>

<svelte:window {onkeydown} />

<div class="save-bar" class:dirty role="region" aria-label="Сохранение">
	<span class="state">
		{#if invalid}
			<span class="error-text">{invalid}</span>
		{:else if dirty}
			<span class="dot"></span>Несохранённые изменения
		{:else}
			<span class="muted">Изменений нет</span>
		{/if}
	</span>
	<kbd class="kbd">Ctrl S</kbd>
	{#if onreset}
		<button class="btn ghost" disabled={!dirty || saving} onclick={onreset}>Сбросить</button>
	{/if}
	<button class="btn primary" disabled={!dirty || !!invalid} aria-busy={saving} onclick={onsave}
		>{saveLabel}</button
	>
</div>

<style>
	.save-bar {
		position: sticky;
		bottom: 16px;
		z-index: 5;
		display: flex;
		align-items: center;
		gap: 10px;
		margin-top: 24px;
		padding: 10px 10px 10px 18px;
		border: 1px solid var(--line);
		border-radius: var(--r-md);
		background: color-mix(in srgb, var(--surface) 92%, transparent);
		backdrop-filter: blur(8px);
	}

	.save-bar.dirty {
		border-color: var(--accent);
		box-shadow: var(--shadow-pop);
	}

	.state {
		display: flex;
		align-items: center;
		gap: 8px;
		margin-right: auto;
		font-size: 13.5px;
	}

	.dot {
		width: 8px;
		height: 8px;
		border-radius: 999px;
		background: var(--accent);
	}

	@media (max-width: 640px) {
		.kbd {
			display: none;
		}
	}
</style>
