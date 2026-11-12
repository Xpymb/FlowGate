<script lang="ts">
	import '../app.css';
	import favicon from '#lib/assets/favicon.svg';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { session } from '#lib/session.svelte.js';
	import JobPanel from '#lib/components/JobPanel.svelte';
	import Toasts from '#lib/components/Toasts.svelte';
	import type { LayoutProps } from './$types';

	let { children }: LayoutProps = $props();

	const links = [
		{ path: '/', title: 'Обзор' },
		{ path: '/xray', title: 'Xray' },
		{ path: '/servers', title: 'Серверы' },
		{ path: '/subscriptions', title: 'Подписки' },
		{ path: '/connections', title: 'Соединения' },
		{ path: '/sources', title: 'Источники' },
		{ path: '/routes', title: 'Маршруты' },
		{ path: '/settings', title: 'Настройки' }
	] as const;

	function isActive(path: string): boolean {
		return path === '/' ? page.url.pathname === '/' : page.url.pathname.startsWith(path);
	}
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
</svelte:head>

<div class="shell">
	<header>
		<a class="brand" href={resolve('/')}>FlowGate</a>
		<nav>
			{#each links as link (link.path)}
				<a href={resolve(link.path)} class:active={isActive(link.path)}>{link.title}</a>
			{/each}
		</nav>
	</header>

	<main>
		{#if session.unauthorized}
			<p class="card error">
				Сессия закончилась или вход не выполнен. Обновите страницу, чтобы войти.
			</p>
		{/if}
		{@render children()}
	</main>
</div>

<JobPanel />
<Toasts />

<style>
	.shell {
		max-width: 1100px;
		margin: 0 auto;
		padding: 0 var(--gap) var(--gap);
	}

	header {
		display: flex;
		align-items: center;
		gap: 24px;
		padding: 12px 0;
		margin-bottom: var(--gap);
		border-bottom: 1px solid var(--border);
		flex-wrap: wrap;
	}

	.brand {
		font-weight: 600;
		font-size: 1.1rem;
		color: var(--text);
		text-decoration: none;
	}

	nav {
		display: flex;
		gap: 16px;
		flex-wrap: wrap;
	}

	nav a {
		color: var(--muted);
		text-decoration: none;
	}

	nav a.active {
		color: var(--text);
		font-weight: 600;
	}

	main > .error {
		margin-bottom: var(--gap);
	}
</style>
