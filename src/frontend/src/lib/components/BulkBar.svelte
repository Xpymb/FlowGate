<script lang="ts">
	import type { Snippet } from 'svelte';
	import Icon from './Icon.svelte';

	interface Props {
		count: number;
		onclear: () => void;
		children: Snippet;
	}

	let { count, onclear, children }: Props = $props();
</script>

{#if count > 0}
	<div class="bulk" role="toolbar" aria-label="Действия с выбранными">
		<span class="count num">Выбрано: {count}</span>
		<div class="row">{@render children()}</div>
		<button class="btn ghost sm icon" aria-label="Снять выбор" onclick={onclear}>
			<Icon name="close" size={16} />
		</button>
	</div>
{/if}

<style>
	.bulk {
		position: fixed;
		left: 50%;
		bottom: 24px;
		z-index: 30;
		display: flex;
		align-items: center;
		gap: 12px;
		max-width: calc(100vw - 32px);
		padding: 8px 8px 8px 18px;
		border-radius: var(--r-lg);
		background: var(--ink);
		color: var(--bg);
		box-shadow: var(--shadow-pop);
		transform: translateX(-50%);
		animation: rise 0.18s var(--ease);
	}

	@media (min-width: 900px) {
		.bulk {
			left: calc(50% + var(--sidebar) / 2);
		}
	}

	.count {
		font-weight: 500;
		white-space: nowrap;
	}

	.bulk :global(.btn) {
		border-color: transparent;
		background: color-mix(in srgb, var(--bg) 14%, transparent);
		color: var(--bg);
	}

	.bulk :global(.btn:hover) {
		background: color-mix(in srgb, var(--bg) 24%, transparent);
	}

	.bulk :global(.btn.danger) {
		background: var(--danger);
		color: #fff;
	}

	@keyframes rise {
		from {
			opacity: 0;
			transform: translate(-50%, 12px);
		}
	}
</style>
