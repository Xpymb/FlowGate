<script lang="ts">
	import { copyText } from '#lib/toast.svelte.js';
	import Icon from './Icon.svelte';

	interface Props {
		value: string;
		/** Что показать вместо значения. */
		text?: string;
		mono?: boolean;
	}

	let { value, text, mono = true }: Props = $props();
</script>

<button
	class="copyable"
	class:mono
	title="Скопировать"
	onclick={(event) => {
		event.stopPropagation();
		void copyText(value, 'Скопировано в буфер обмена');
	}}
>
	<span class="text">{text ?? value}</span>
	<Icon name="copy" size={13} />
</button>

<style>
	.copyable {
		display: inline-flex;
		align-items: center;
		gap: 6px;
		max-width: 100%;
		padding: 1px 4px;
		margin: -1px -4px;
		border-radius: 4px;
		color: inherit;
		text-align: left;
	}

	.copyable.mono .text {
		font-family: var(--mono);
		font-size: 12.5px;
	}

	.text {
		min-width: 0;
		overflow-wrap: anywhere;
	}

	.copyable :global(.icon) {
		color: var(--faint);
		opacity: 0;
		transition: opacity 0.12s;
	}

	.copyable:hover {
		background: var(--hover);
	}

	.copyable:hover :global(.icon),
	.copyable:focus-visible :global(.icon) {
		opacity: 1;
	}
</style>
