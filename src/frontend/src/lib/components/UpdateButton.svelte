<script lang="ts">
	import type { components } from '#lib/api/schema.js';

	type UpdateVia = components['schemas']['UpdateVia'];

	interface Props {
		/** Текст основной кнопки: обновление по настройкам. */
		label: string;
		/** Идёт обновление: кнопки заблокированы, текст — «Обновляется…». */
		busy?: boolean;
		disabled?: boolean;
		small?: boolean;
		secondary?: boolean;
		onrun: (via: UpdateVia) => void;
	}

	let {
		label,
		busy = false,
		disabled = false,
		small = false,
		secondary = false,
		onrun
	}: Props = $props();

	let open = $state(false);
	let root = $state<HTMLElement>();

	function run(via: UpdateVia) {
		open = false;
		onrun(via);
	}

	function onWindowClick(event: MouseEvent) {
		if (open && root && !root.contains(event.target as Node)) open = false;
	}
</script>

<svelte:window
	onclick={onWindowClick}
	onkeydown={(event) => event.key === 'Escape' && (open = false)}
/>

<span class="split" bind:this={root}>
	<button
		class:small
		class:secondary
		class="main"
		disabled={busy || disabled}
		onclick={() => run('Auto')}
		title="По настройкам: через прокси обновлений, если он включён"
	>
		{busy ? 'Обновляется…' : label}
	</button>
	<button
		class:small
		class:secondary
		class="toggle"
		disabled={busy || disabled}
		aria-label="Как загружать"
		aria-haspopup="menu"
		aria-expanded={open}
		onclick={() => (open = !open)}>▾</button
	>
	{#if open}
		<span class="menu" role="menu">
			<button role="menuitem" class="secondary small" onclick={() => run('Proxy')}>
				Принудительно через прокси
			</button>
			<button role="menuitem" class="secondary small" onclick={() => run('Direct')}>
				Принудительно без прокси
			</button>
		</span>
	{/if}
</span>

<style>
	.split {
		position: relative;
		display: inline-flex;
	}

	.main {
		border-top-right-radius: 0;
		border-bottom-right-radius: 0;
	}

	.toggle {
		border-top-left-radius: 0;
		border-bottom-left-radius: 0;
		border-left: 1px solid color-mix(in srgb, currentColor 25%, transparent);
		padding-left: 8px;
		padding-right: 8px;
	}

	.menu {
		position: absolute;
		top: calc(100% + 4px);
		right: 0;
		z-index: 10;
		display: flex;
		flex-direction: column;
		gap: 4px;
		padding: 6px;
		background: var(--surface);
		border: 1px solid var(--border);
		border-radius: var(--radius);
		box-shadow: 0 4px 16px rgb(0 0 0 / 0.12);
		white-space: nowrap;
	}

	.menu button {
		text-align: left;
	}
</style>
