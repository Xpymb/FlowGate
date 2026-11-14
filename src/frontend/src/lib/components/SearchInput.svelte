<script lang="ts">
	import { hotkey } from '#lib/hotkeys.js';
	import Icon from './Icon.svelte';

	interface Props {
		value: string;
		placeholder?: string;
		/** Подпись для экранного диктора. */
		label?: string;
		/** Клавиша `/` ставит фокус в это поле. */
		shortcut?: boolean;
		oninput?: (value: string) => void;
	}

	let {
		value = $bindable(),
		placeholder = 'Поиск',
		label = 'Поиск',
		shortcut = true,
		oninput
	}: Props = $props();

	let input = $state<HTMLInputElement>();

	$effect(() => {
		if (!shortcut) return;
		return hotkey('/', () => input?.focus());
	});

	function update(next: string) {
		value = next;
		oninput?.(next);
	}
</script>

<div class="search">
	<Icon name="search" size={16} />
	<input
		bind:this={input}
		type="search"
		aria-label={label}
		{placeholder}
		{value}
		oninput={(event) => update(event.currentTarget.value)}
		onkeydown={(event) => {
			if (event.key === 'Escape' && value) {
				event.preventDefault();
				update('');
			}
		}}
	/>
	{#if shortcut && !value}<kbd>/</kbd>{/if}
</div>

<style>
	.search {
		position: relative;
		display: flex;
		align-items: center;
		width: 300px;
		max-width: 100%;
	}

	.search > :global(.icon) {
		position: absolute;
		left: 11px;
		color: var(--faint);
		pointer-events: none;
	}

	input {
		padding-left: 34px !important;
		padding-right: 34px !important;
	}

	input::-webkit-search-cancel-button {
		cursor: pointer;
	}

	kbd {
		position: absolute;
		right: 9px;
		pointer-events: none;
	}
</style>
