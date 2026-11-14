<script lang="ts" module>
	import type { IconName } from '../Icon.svelte';

	/** empty — ещё не выбрано, filled — выбрано в мастере, running/ok/failed — ход запуска. */
	export type PathTone = 'empty' | 'filled' | 'running' | 'ok' | 'failed';

	export interface PathNode {
		key: string;
		icon: IconName;
		title: string;
		value: string;
		tone: PathTone;
	}
</script>

<script lang="ts">
	import Icon from '../Icon.svelte';

	interface Props {
		nodes: PathNode[];
	}

	let { nodes }: Props = $props();
</script>

<!-- Схема дублирует то, что сказано в шагах мастера, поэтому скрыта от чтения с экрана. -->
<ol class="rail" aria-hidden="true">
	{#each nodes as node, index (node.key)}
		{#if index > 0}
			<li
				class="link"
				class:lit={nodes[index - 1].tone === 'ok' || nodes[index - 1].tone === 'filled'}
				class:moving={nodes[index - 1].tone === 'ok' && node.tone === 'ok'}
			></li>
		{/if}
		<li class="node {node.tone}">
			<span class="mark">
				{#if node.tone === 'ok'}
					<Icon name="check" size={15} />
				{:else if node.tone === 'failed'}
					<Icon name="alert" size={15} />
				{:else}
					<Icon name={node.icon} size={15} />
				{/if}
			</span>
			<span class="text">
				<span class="title">{node.title}</span>
				<span class="value">{node.value}</span>
			</span>
		</li>
	{/each}
</ol>

<style>
	.rail {
		display: flex;
		flex-direction: column;
		margin: 0;
		padding: 0;
		list-style: none;
	}

	.node {
		display: grid;
		grid-template-columns: 32px minmax(0, 1fr);
		align-items: center;
		gap: 12px;
	}

	.mark {
		display: grid;
		place-items: center;
		width: 32px;
		height: 32px;
		border: 1px dashed var(--line-strong);
		border-radius: 9px;
		background: var(--surface);
		color: var(--faint);
		transition:
			background-color 0.25s,
			border-color 0.25s,
			color 0.25s;
	}

	.text {
		display: grid;
		min-width: 0;
	}

	.title {
		color: var(--muted);
		font-size: 12.5px;
	}

	.value {
		overflow: hidden;
		color: var(--faint);
		font-weight: 500;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.filled .mark {
		border-style: solid;
		border-color: var(--accent);
		color: var(--accent-text);
	}

	.filled .value,
	.running .value,
	.ok .value {
		color: var(--ink);
	}

	.running .mark {
		border-style: solid;
		border-color: var(--accent);
		background: var(--accent-soft);
		color: var(--accent-text);
		animation: beat 1.2s var(--ease) infinite;
	}

	.ok .mark {
		border: 1px solid transparent;
		background: var(--ok-soft);
		color: var(--ok);
	}

	.failed .mark {
		border: 1px solid transparent;
		background: var(--danger-soft);
		color: var(--danger);
	}

	.failed .value {
		color: var(--danger);
	}

	/* Связь между узлами: под центром отметки. */
	.link {
		position: relative;
		height: 22px;
		margin-left: 15px;
		width: 2px;
		background: var(--line);
		transition: background-color 0.25s;
	}

	.link.lit {
		background: color-mix(in srgb, var(--accent) 45%, transparent);
	}

	.link.moving {
		background: repeating-linear-gradient(180deg, var(--accent) 0 6px, transparent 6px 11px);
		background-size: 2px 22px;
		animation: flow 0.9s linear infinite;
	}

	@keyframes flow {
		to {
			background-position: 0 22px;
		}
	}

	@keyframes beat {
		50% {
			box-shadow: 0 0 0 4px var(--accent-soft);
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.link.moving,
		.running .mark {
			animation: none;
		}
	}

	/* Узкий экран: путь в одну строку, только отметки. */
	@media (max-width: 720px) {
		.rail {
			flex-direction: row;
			align-items: center;
		}

		.node {
			display: block;
		}

		.text {
			display: none;
		}

		.link {
			flex: 1;
			width: auto;
			height: 2px;
			margin: 0;
		}

		.link.moving {
			background: repeating-linear-gradient(90deg, var(--accent) 0 6px, transparent 6px 11px);
			background-size: 22px 2px;
			animation-name: flow-right;
		}
	}

	@keyframes flow-right {
		to {
			background-position: 22px 0;
		}
	}
</style>
