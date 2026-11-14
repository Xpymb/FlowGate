<script lang="ts" generics="T">
	import type { Snippet } from 'svelte';
	import { errorMessage } from '#lib/api/errors.js';
	import type { Resource } from '#lib/resource.svelte.js';
	import Icon from './Icon.svelte';

	interface Props {
		resource: Resource<T>;
		/** Сколько строк-заглушек показать, пока данных нет. */
		rows?: number;
		children: Snippet<[T]>;
	}

	let { resource, rows = 4, children }: Props = $props();
</script>

{#if resource.data !== undefined}
	{#if resource.error}
		<div class="stale" role="status">
			<Icon name="alert" size={16} />
			<span>Не удалось обновить данные: {errorMessage(resource.error)}</span>
			<button class="btn ghost sm" onclick={() => resource.reload()}>Повторить</button>
		</div>
	{/if}
	{@render children(resource.data)}
{:else if resource.error}
	<div class="failed panel" role="alert">
		<Icon name="alert" size={20} />
		<div>
			<h2>Не удалось загрузить</h2>
			<p class="muted">{errorMessage(resource.error)}</p>
		</div>
		<button class="btn" aria-busy={resource.loading} onclick={() => resource.reload()}>
			Повторить
		</button>
	</div>
{:else}
	<div class="skeleton panel" aria-busy="true" aria-label="Загрузка">
		{#each [...Array(rows).keys()] as index (index)}
			<div class="bar" style:width="{88 - ((index * 17) % 40)}%"></div>
		{/each}
	</div>
{/if}

<style>
	.stale {
		display: flex;
		align-items: center;
		gap: 8px;
		margin-bottom: 12px;
		padding: 6px 6px 6px 12px;
		border-radius: var(--r-sm);
		background: var(--warn-soft);
		color: var(--warn);
		font-size: 13px;
	}

	.stale span {
		flex: 1;
	}

	.failed {
		display: flex;
		align-items: center;
		gap: 16px;
		padding: 20px;
		color: var(--danger);
	}

	.failed div {
		flex: 1;
		color: var(--ink);
	}

	.skeleton {
		display: grid;
		gap: 18px;
		padding: 22px 20px;
	}

	.bar {
		height: 12px;
		border-radius: 4px;
		background: linear-gradient(90deg, var(--hover) 30%, var(--surface-2) 50%, var(--hover) 70%);
		background-size: 300% 100%;
		animation: shimmer 1.4s ease-in-out infinite;
	}

	@keyframes shimmer {
		from {
			background-position: 100% 0;
		}
		to {
			background-position: 0 0;
		}
	}
</style>
