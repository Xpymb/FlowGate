<script lang="ts" generics="T">
	import type { Snippet } from 'svelte';
	import { errorMessage } from '#lib/api/errors.js';
	import type { Resource } from '#lib/resource.svelte.js';

	interface Props {
		resource: Resource<T>;
		children: Snippet<[T]>;
	}

	let { resource, children }: Props = $props();
</script>

{#if resource.data !== undefined}
	{@render children(resource.data)}
	{#if resource.error}
		<p class="error">Не удалось обновить: {errorMessage(resource.error)}</p>
	{/if}
{:else if resource.error}
	<p class="error">
		{errorMessage(resource.error)}
		<button class="secondary" onclick={() => resource.reload()}>Повторить</button>
	</p>
{:else}
	<p class="muted">Загрузка…</p>
{/if}
