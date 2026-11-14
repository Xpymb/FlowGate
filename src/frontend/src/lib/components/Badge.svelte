<script lang="ts">
	import type { Snippet } from 'svelte';
	import type { Tone } from '#lib/labels.js';

	interface Props {
		tone?: Tone;
		/** Точка-индикатор слева — для состояний, а не для меток. */
		dot?: boolean;
		title?: string;
		children: Snippet;
	}

	let { tone = 'muted', dot = false, title, children }: Props = $props();
</script>

<span class="badge {tone}" class:dot {title}>{@render children()}</span>

<style>
	.badge {
		display: inline-flex;
		align-items: center;
		gap: 6px;
		height: 22px;
		padding: 0 8px;
		border-radius: 5px;
		font-size: 12.5px;
		font-weight: 500;
		line-height: 1;
		white-space: nowrap;
		vertical-align: middle;
	}

	.dot::before {
		content: '';
		width: 6px;
		height: 6px;
		border-radius: 999px;
		background: currentColor;
	}

	.ok {
		background: var(--ok-soft);
		color: var(--ok);
	}

	.warn {
		background: var(--warn-soft);
		color: var(--warn);
	}

	.danger {
		background: var(--danger-soft);
		color: var(--danger);
	}

	.muted {
		background: var(--hover);
		color: var(--muted);
	}

	.accent {
		background: var(--accent-soft);
		color: var(--accent-text);
	}
</style>
