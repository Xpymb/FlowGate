<script lang="ts">
	import type { Snippet } from 'svelte';
	import Icon from './Icon.svelte';

	interface Props {
		title: string;
		description?: string;
		/** Ссылка «назад» для страниц объекта: адрес уже через resolve(). */
		back?: { href: string; label: string };
		/** Метки рядом с заголовком (состояние объекта). */
		meta?: Snippet;
		actions?: Snippet;
	}

	let { title, description, back, meta, actions }: Props = $props();
</script>

<svelte:head>
	<title>{title} · FlowGate</title>
</svelte:head>

<header class="page-header">
	{#if back}
		<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- href уже через resolve() -->
		<a class="back" href={back.href}><Icon name="arrowLeft" size={15} />{back.label}</a>
	{/if}
	<div class="line">
		<div class="titles">
			<div class="title-row">
				<h1>{title}</h1>
				{#if meta}{@render meta()}{/if}
			</div>
			{#if description}<p class="description">{description}</p>{/if}
		</div>
		{#if actions}<div class="actions">{@render actions()}</div>{/if}
	</div>
</header>

<style>
	.page-header {
		margin-bottom: 28px;
	}

	.back {
		display: inline-flex;
		align-items: center;
		gap: 6px;
		margin-bottom: 12px;
		color: var(--muted);
		font-size: 13px;
	}

	.back:hover {
		color: var(--ink);
		text-decoration: none;
	}

	/* Кнопки выровнены по строке заголовка: высота описания не сдвигает их от страницы к странице. */
	.line {
		display: flex;
		align-items: flex-start;
		gap: 16px 24px;
		flex-wrap: wrap;
	}

	.titles {
		flex: 1 1 360px;
		min-width: 0;
	}

	.title-row {
		display: flex;
		align-items: center;
		gap: 12px;
		flex-wrap: wrap;
	}

	h1 {
		overflow-wrap: anywhere;
	}

	.description {
		max-width: 68ch;
		margin-top: 6px;
		color: var(--muted);
	}

	.actions {
		margin-top: -2px;
		display: flex;
		align-items: center;
		gap: 8px;
		flex-wrap: wrap;
	}
</style>
