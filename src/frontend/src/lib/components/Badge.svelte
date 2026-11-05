<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		tone?: 'ok' | 'warn' | 'danger' | 'muted' | 'accent';
		title?: string;
		children: Snippet;
	}

	let { tone = 'muted', title, children }: Props = $props();
</script>

<span class="badge {tone}" {title}>{@render children()}</span>

<style>
	.badge {
		display: inline-block;
		padding: 0 8px;
		border-radius: 999px;
		font-size: 0.82em;
		line-height: 1.7;
		border: 1px solid currentColor;
		white-space: nowrap;
	}

	.ok {
		color: var(--ok);
	}

	.warn {
		color: var(--warn);
	}

	.danger {
		color: var(--danger);
	}

	.muted {
		color: var(--muted);
	}

	.accent {
		color: var(--accent);
	}
</style>
