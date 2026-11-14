<script lang="ts">
	import type { Snippet } from 'svelte';
	import Icon, { type IconName } from './Icon.svelte';

	interface Props {
		icon?: IconName;
		title: string;
		text?: string;
		/** Без рамки — внутри уже существующей панели. */
		bare?: boolean;
		children?: Snippet;
	}

	let { icon = 'info', title, text, bare = false, children }: Props = $props();
</script>

<div class="empty" class:panel={!bare}>
	<span class="mark"><Icon name={icon} size={22} /></span>
	<h2>{title}</h2>
	{#if text}<p>{text}</p>{/if}
	{#if children}<div class="row">{@render children()}</div>{/if}
</div>

<style>
	.empty {
		display: grid;
		justify-items: center;
		gap: 6px;
		padding: 48px 24px;
		text-align: center;
	}

	.mark {
		display: grid;
		place-items: center;
		width: 48px;
		height: 48px;
		margin-bottom: 8px;
		border-radius: 12px;
		background: var(--accent-soft);
		color: var(--accent-text);
	}

	p {
		max-width: 46ch;
		color: var(--muted);
	}

	.row {
		justify-content: center;
		margin-top: 12px;
	}
</style>
