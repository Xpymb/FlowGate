<script lang="ts">
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '#lib/api/client.js';
	import { unwrap } from '#lib/api/errors.js';
	import { label, xrayStates } from '#lib/labels.js';
	import { isActive, navGroups, overviewLink, settingsLink, type NavLink } from '#lib/nav.js';
	import { Resource } from '#lib/resource.svelte.js';
	import { cycleTheme, theme } from '#lib/theme.svelte.js';
	import Icon from './Icon.svelte';
	import Logo from './Logo.svelte';

	interface Props {
		/** Мобильный режим: панель выезжает поверх контента. */
		open: boolean;
		onpalette: () => void;
		onclose: () => void;
	}

	let { open, onpalette, onclose }: Props = $props();

	const storageKey = 'flowgate-nav-collapsed';
	const xray = new Resource(() => unwrap(api.GET('/api/xray')), { live: ['xray'] });
	const system = new Resource(() => unwrap(api.GET('/api/system')));

	let collapsed = $state<Record<string, boolean>>(readCollapsed());

	const pathname = $derived(page.url.pathname);
	const xrayState = $derived(xray.data ? label(xrayStates, xray.data.state) : undefined);
	const themeTitle = $derived(
		theme.mode === 'system'
			? 'Тема как в системе'
			: theme.mode === 'light'
				? 'Светлая тема'
				: 'Тёмная тема'
	);

	function readCollapsed(): Record<string, boolean> {
		try {
			return JSON.parse(localStorage.getItem(storageKey) ?? '{}') as Record<string, boolean>;
		} catch {
			return {};
		}
	}

	function toggleGroup(id: string) {
		collapsed[id] = !collapsed[id];
		try {
			localStorage.setItem(storageKey, JSON.stringify(collapsed));
		} catch {
			// без хранилища состояние живёт до перезагрузки
		}
	}

	function groupActive(links: NavLink[]): boolean {
		return links.some((link) => isActive(link.path, pathname));
	}
</script>

{#snippet item(link: NavLink, nested = false)}
	{@const active = isActive(link.path, pathname)}
	<a
		class="item"
		class:nested
		class:active
		href={resolve(link.path)}
		aria-current={active ? 'page' : undefined}
		onclick={onclose}
	>
		{#if !nested}<Icon name={link.icon} />{/if}
		<span>{link.title}</span>
	</a>
{/snippet}

<nav class="sidebar" class:open aria-label="Разделы">
	<div class="top">
		<a class="brand" href={resolve('/')} onclick={onclose}>
			<Logo />
			<span>FlowGate</span>
		</a>
		<button class="btn ghost icon sm close" aria-label="Закрыть меню" onclick={onclose}>
			<Icon name="close" />
		</button>
	</div>

	<button class="palette" onclick={onpalette}>
		<Icon name="search" size={16} />
		<span>Поиск и команды</span>
		<kbd>Ctrl K</kbd>
	</button>

	<div class="scroll">
		{@render item(overviewLink)}

		{#each navGroups as group (group.id)}
			{@const active = groupActive(group.links)}
			{@const expanded = active || !collapsed[group.id]}
			<div class="group" class:active>
				<button
					class="item group-head"
					aria-expanded={expanded}
					aria-controls="nav-{group.id}"
					disabled={active}
					title={active ? 'Раздел открыт: в нём текущая страница' : undefined}
					onclick={() => toggleGroup(group.id)}
				>
					<Icon name={group.icon} />
					<span>{group.title}</span>
					<Icon name="chevronDown" size={16} class="chevron" />
				</button>
				<div class="group-body" id="nav-{group.id}" class:expanded>
					<div class="group-inner">
						{#each group.links as link (link.path)}
							{@render item(link, true)}
						{/each}
					</div>
				</div>
			</div>
		{/each}
	</div>

	<div class="bottom">
		{@render item(settingsLink)}
		<div class="status">
			<a class="xray" href={resolve('/xray')} onclick={onclose}>
				<span class="led {xrayState?.tone ?? 'muted'}" aria-hidden="true"></span>
				<span class="xray-text">
					<span>Xray {xrayState?.label ?? '…'}</span>
					<span class="faint small">
						{xray.data?.version ?? '—'} · FlowGate {system.data?.version ?? '—'}
					</span>
				</span>
			</a>
			<button
				class="btn ghost icon sm"
				title={themeTitle}
				aria-label={themeTitle}
				onclick={cycleTheme}
			>
				<Icon
					name={theme.mode === 'system' ? 'monitor' : theme.mode === 'light' ? 'sun' : 'moon'}
					size={16}
				/>
			</button>
		</div>
	</div>
</nav>

<style>
	.sidebar {
		position: sticky;
		top: 0;
		display: flex;
		flex-direction: column;
		width: var(--sidebar);
		height: 100dvh;
		padding: 14px 12px 12px;
		border-right: 1px solid var(--line);
		background: var(--surface);
	}

	.top {
		display: flex;
		align-items: center;
		padding: 2px 6px 14px;
	}

	.brand {
		display: flex;
		align-items: center;
		gap: 10px;
		margin-right: auto;
		color: var(--ink);
		font-size: 16px;
		font-weight: 650;
		letter-spacing: -0.01em;
	}

	.brand:hover {
		text-decoration: none;
	}

	.close {
		display: none;
	}

	.palette {
		display: flex;
		align-items: center;
		gap: 8px;
		height: 34px;
		margin-bottom: 14px;
		padding: 0 8px 0 10px;
		border: 1px solid var(--line);
		border-radius: var(--r-sm);
		background: var(--surface-2);
		color: var(--faint);
		font-size: 13px;
	}

	.palette span {
		flex: 1;
		text-align: left;
	}

	.palette:hover {
		border-color: var(--line-strong);
		color: var(--muted);
	}

	.scroll {
		flex: 1;
		display: grid;
		align-content: start;
		gap: 2px;
		overflow-y: auto;
		margin: 0 -4px;
		padding: 0 4px;
	}

	.item {
		display: flex;
		align-items: center;
		gap: 10px;
		width: 100%;
		height: 36px;
		padding: 0 10px;
		border-radius: var(--r-sm);
		color: var(--ink-2);
		font-weight: 500;
		text-align: left;
		text-decoration: none;
	}

	.item :global(.icon) {
		color: var(--faint);
	}

	.item:hover {
		background: var(--hover);
		color: var(--ink);
		text-decoration: none;
	}

	.item.active {
		background: var(--accent-soft);
		color: var(--accent-text);
	}

	.item.active :global(.icon) {
		color: var(--accent-text);
	}

	.group {
		margin-top: 10px;
	}

	.group-head span {
		flex: 1;
	}

	.group-head:disabled {
		cursor: default;
	}

	.group-head:disabled:hover {
		background: none;
	}

	.group.active .group-head {
		color: var(--ink);
	}

	.group-head :global(.chevron) {
		transition: transform 0.18s var(--ease);
	}

	.group-head[aria-expanded='false'] :global(.chevron) {
		transform: rotate(-90deg);
	}

	.group.active .group-head :global(.chevron) {
		opacity: 0;
	}

	/* Аккордеон: плавная высота через grid-rows. */
	.group-body {
		display: grid;
		grid-template-rows: 0fr;
		transition: grid-template-rows 0.2s var(--ease);
	}

	.group-body.expanded {
		grid-template-rows: 1fr;
	}

	.group-inner {
		display: grid;
		gap: 1px;
		overflow: hidden;
		margin-left: 19px;
		padding-left: 10px;
		border-left: 1px solid var(--line);
	}

	.group-body:not(.expanded) .group-inner {
		visibility: hidden;
		transition: visibility 0s 0.2s;
	}

	.item.nested {
		height: 32px;
		font-weight: 450;
		color: var(--muted);
	}

	.item.nested.active {
		color: var(--accent-text);
		font-weight: 550;
	}

	.bottom {
		display: grid;
		gap: 8px;
		padding-top: 10px;
		border-top: 1px solid var(--line);
	}

	.status {
		display: flex;
		align-items: center;
		gap: 4px;
	}

	.xray {
		flex: 1;
		display: flex;
		align-items: center;
		gap: 10px;
		min-width: 0;
		padding: 6px 10px;
		border-radius: var(--r-sm);
		color: var(--ink-2);
		font-size: 13px;
		line-height: 1.3;
	}

	.xray:hover {
		background: var(--hover);
		text-decoration: none;
	}

	.xray-text {
		display: grid;
		min-width: 0;
	}

	.xray-text span {
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.led {
		flex: none;
		width: 8px;
		height: 8px;
		border-radius: 999px;
		background: var(--faint);
	}

	.led.ok {
		background: var(--ok);
		box-shadow: 0 0 0 3px var(--ok-soft);
	}

	.led.warn,
	.led.accent {
		background: var(--warn);
		box-shadow: 0 0 0 3px var(--warn-soft);
	}

	.led.danger {
		background: var(--danger);
		box-shadow: 0 0 0 3px var(--danger-soft);
	}

	@media (max-width: 899px) {
		.sidebar {
			position: fixed;
			left: 0;
			z-index: 45;
			width: min(300px, 86vw);
			box-shadow: var(--shadow-pop);
			transform: translateX(-100%);
			visibility: hidden;
			transition:
				transform 0.22s var(--ease),
				visibility 0s 0.22s;
		}

		.sidebar.open {
			transform: none;
			visibility: visible;
			transition: transform 0.22s var(--ease);
		}

		.close {
			display: inline-flex;
		}

		.palette kbd {
			display: none;
		}
	}
</style>
