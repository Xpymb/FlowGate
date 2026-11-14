<script lang="ts">
	import '../app.css';
	import favicon from '#lib/assets/favicon.svg';
	import { afterNavigate } from '$app/navigation';
	import { page } from '$app/state';
	import { dispatchHotkey, isTyping } from '#lib/hotkeys.js';
	import { currentLink } from '#lib/nav.js';
	import { session } from '#lib/session.svelte.js';
	import CommandPalette from '#lib/components/CommandPalette.svelte';
	import ConfirmHost from '#lib/components/ConfirmHost.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import JobPanel from '#lib/components/JobPanel.svelte';
	import Logo from '#lib/components/Logo.svelte';
	import ShortcutsHelp from '#lib/components/ShortcutsHelp.svelte';
	import Sidebar from '#lib/components/Sidebar.svelte';
	import Toasts from '#lib/components/Toasts.svelte';
	import type { LayoutProps } from './$types';

	let { children }: LayoutProps = $props();

	let drawerOpen = $state(false);
	let paletteOpen = $state(false);
	let helpOpen = $state(false);

	const section = $derived(currentLink(page.url.pathname));

	afterNavigate(() => {
		drawerOpen = false;
	});

	function onkeydown(event: KeyboardEvent) {
		if ((event.ctrlKey || event.metaKey) && event.code === 'KeyK') {
			event.preventDefault();
			paletteOpen = !paletteOpen;
			return;
		}
		if (event.key === 'Escape' && drawerOpen) {
			drawerOpen = false;
			return;
		}
		const question = event.key === '?' || (event.shiftKey && event.code === 'Slash');
		if (question && !isTyping(event.target) && !document.querySelector('dialog[open]')) {
			event.preventDefault();
			helpOpen = true;
			return;
		}
		dispatchHotkey(event);
	}
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
</svelte:head>

<svelte:window {onkeydown} />

<a class="skip" href="#content">К содержимому</a>

<div class="app">
	<Sidebar
		open={drawerOpen}
		onpalette={() => {
			drawerOpen = false;
			paletteOpen = true;
		}}
		onclose={() => (drawerOpen = false)}
	/>
	{#if drawerOpen}
		<button class="scrim" aria-label="Закрыть меню" onclick={() => (drawerOpen = false)}></button>
	{/if}

	<div class="main-column">
		<header class="mobile-bar">
			<button
				class="btn ghost icon"
				aria-label="Меню"
				aria-expanded={drawerOpen}
				onclick={() => (drawerOpen = true)}
			>
				<Icon name="menu" />
			</button>
			<Logo />
			<span class="section">{section?.title ?? 'FlowGate'}</span>
			<button
				class="btn ghost icon"
				aria-label="Поиск и команды"
				onclick={() => (paletteOpen = true)}
			>
				<Icon name="search" />
			</button>
		</header>

		{#if session.unauthorized}
			<div class="session" role="alert">
				<Icon name="lock" size={16} />
				<span>Сессия истекла. Обновите страницу для повторного входа.</span>
				<button class="btn sm" onclick={() => location.reload()}>Обновить</button>
			</div>
		{/if}

		<main id="content" tabindex="-1">
			{@render children()}
		</main>
	</div>
</div>

<CommandPalette bind:open={paletteOpen} />
<ShortcutsHelp bind:open={helpOpen} />
<ConfirmHost />
<JobPanel />
<Toasts />

<style>
	.skip {
		position: fixed;
		top: 8px;
		left: 8px;
		z-index: 100;
		padding: 8px 12px;
		border-radius: var(--r-sm);
		background: var(--accent);
		color: var(--accent-ink);
		transform: translateY(-200%);
	}

	.skip:focus {
		transform: none;
	}

	.app {
		display: flex;
		min-height: 100dvh;
	}

	.main-column {
		flex: 1;
		min-width: 0;
	}

	main {
		max-width: 1280px;
		margin: 0 auto;
		padding: 40px 48px 96px;
		outline: none;
	}

	.mobile-bar {
		display: none;
	}

	.scrim {
		display: none;
	}

	.session {
		display: flex;
		align-items: center;
		gap: 10px;
		padding: 10px 48px;
		background: var(--warn-soft);
		color: var(--warn);
		font-weight: 500;
	}

	.session span {
		flex: 1;
	}

	@media (max-width: 1200px) {
		main {
			padding: 32px 32px 96px;
		}
	}

	@media (max-width: 899px) {
		.mobile-bar {
			position: sticky;
			top: 0;
			z-index: 20;
			display: flex;
			align-items: center;
			gap: 10px;
			height: 56px;
			padding: 0 8px;
			border-bottom: 1px solid var(--line);
			background: color-mix(in srgb, var(--surface) 90%, transparent);
			backdrop-filter: blur(10px);
		}

		.section {
			flex: 1;
			font-weight: 600;
		}

		.scrim {
			position: fixed;
			inset: 0;
			z-index: 44;
			display: block;
			background: var(--backdrop);
			animation: fade 0.2s;
		}

		main {
			padding: 24px 16px 96px;
		}

		.session {
			padding: 10px 16px;
		}
	}

	@keyframes fade {
		from {
			opacity: 0;
		}
	}
</style>
