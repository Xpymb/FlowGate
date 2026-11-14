<script lang="ts" module>
	import type { IconName } from './Icon.svelte';

	export interface MenuItem {
		label: string;
		icon?: IconName;
		danger?: boolean;
		disabled?: boolean;
		/** Подсказка справа, например горячая клавиша. */
		hint?: string;
		onclick?: () => void;
		href?: string;
	}

	/** Разделитель между группами пунктов. */
	export type MenuEntry = MenuItem | 'separator';
</script>

<script lang="ts">
	import { tick } from 'svelte';
	import Icon from './Icon.svelte';

	interface Props {
		items: MenuEntry[];
		/** Подпись кнопки; без неё — кнопка «⋯». */
		label?: string;
		/** Доступное имя кнопки-иконки. */
		ariaLabel?: string;
		icon?: IconName;
		small?: boolean;
		/** Классы кнопки-триггера, по умолчанию — призрачная. */
		buttonClass?: string;
		disabled?: boolean;
		align?: 'start' | 'end';
	}

	let {
		items,
		label,
		ariaLabel = 'Действия',
		icon = label ? 'chevronDown' : 'more',
		small = true,
		buttonClass = 'ghost',
		disabled = false,
		align = 'end'
	}: Props = $props();

	let open = $state(false);
	let trigger = $state<HTMLButtonElement>();
	let menu = $state<HTMLDivElement>();
	let position = $state({ top: 0, left: 0, up: false });

	async function toggle() {
		if (open) {
			open = false;
			return;
		}
		const rect = trigger!.getBoundingClientRect();
		const up = rect.bottom + 260 > window.innerHeight && rect.top > 260;
		position = {
			top: up ? rect.top - 4 : rect.bottom + 4,
			left: align === 'end' ? rect.right : rect.left,
			up
		};
		open = true;
		await tick();
		focusItem(0);
	}

	function menuItems(): HTMLElement[] {
		return [
			...(menu?.querySelectorAll<HTMLElement>('[role="menuitem"]:not([aria-disabled="true"])') ??
				[])
		];
	}

	function focusItem(index: number) {
		const list = menuItems();
		if (list.length === 0) return;
		list[(index + list.length) % list.length].focus();
	}

	function close(returnFocus = true) {
		open = false;
		if (returnFocus) trigger?.focus();
	}

	function onkeydown(event: KeyboardEvent) {
		const list = menuItems();
		const current = list.indexOf(document.activeElement as HTMLElement);
		if (event.key === 'ArrowDown') focusItem(current + 1);
		else if (event.key === 'ArrowUp') focusItem(current - 1);
		else if (event.key === 'Home') focusItem(0);
		else if (event.key === 'End') focusItem(list.length - 1);
		else if (event.key === 'Escape') close();
		else if (event.key === 'Tab') close(false);
		else return;
		event.preventDefault();
		event.stopPropagation();
	}

	function run(item: MenuItem) {
		if (item.disabled) return;
		close();
		item.onclick?.();
	}

	function onWindowPointer(event: PointerEvent) {
		if (!open) return;
		const target = event.target as Node;
		if (menu?.contains(target) || trigger?.contains(target)) return;
		open = false;
	}
</script>

<svelte:window
	onpointerdown={onWindowPointer}
	onresize={() => (open = false)}
	onscrollcapture={() => (open = false)}
/>

<button
	bind:this={trigger}
	class="btn {buttonClass}"
	class:sm={small}
	class:icon={!label}
	aria-label={label ? undefined : ariaLabel}
	aria-haspopup="menu"
	aria-expanded={open}
	{disabled}
	onclick={toggle}
>
	{#if label}{label}{/if}
	<Icon name={icon} size={label ? 15 : 18} />
</button>

{#if open}
	<div
		bind:this={menu}
		class="menu"
		class:up={position.up}
		class:end={align === 'end'}
		role="menu"
		tabindex="-1"
		style:top="{position.top}px"
		style:left="{position.left}px"
		{onkeydown}
	>
		{#each items as item, index (index)}
			{#if item === 'separator'}
				<div class="separator" role="separator"></div>
			{:else if item.href && !item.disabled}
				<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- href приходит уже через resolve() -->
				<a role="menuitem" class:danger={item.danger} href={item.href} onclick={() => close(false)}>
					{#if item.icon}<Icon name={item.icon} size={16} />{/if}
					<span>{item.label}</span>
					{#if item.hint}<span class="hint">{item.hint}</span>{/if}
				</a>
			{:else}
				<button
					role="menuitem"
					class:danger={item.danger}
					aria-disabled={item.disabled}
					tabindex="-1"
					onclick={() => run(item)}
				>
					{#if item.icon}<Icon name={item.icon} size={16} />{/if}
					<span>{item.label}</span>
					{#if item.hint}<span class="hint">{item.hint}</span>{/if}
				</button>
			{/if}
		{/each}
	</div>
{/if}

<style>
	.menu {
		position: fixed;
		z-index: 50;
		min-width: 200px;
		max-width: 320px;
		padding: 4px;
		border: 1px solid var(--line);
		border-radius: var(--r-md);
		background: var(--surface);
		box-shadow: var(--shadow-pop);
		animation: appear 0.12s var(--ease);
	}

	.menu.end {
		transform: translateX(-100%);
	}

	.menu.up {
		translate: 0 -100%;
	}

	[role='menuitem'] {
		display: flex;
		align-items: center;
		gap: 10px;
		width: 100%;
		min-height: 34px;
		padding: 0 10px;
		border-radius: var(--r-sm);
		color: var(--ink);
		font-size: 13.5px;
		text-align: left;
		text-decoration: none;
		white-space: nowrap;
	}

	[role='menuitem'] :global(.icon) {
		color: var(--muted);
	}

	[role='menuitem']:hover,
	[role='menuitem']:focus-visible {
		background: var(--hover);
		outline: none;
		text-decoration: none;
	}

	[role='menuitem'].danger,
	[role='menuitem'].danger :global(.icon) {
		color: var(--danger);
	}

	[role='menuitem'].danger:hover,
	[role='menuitem'].danger:focus-visible {
		background: var(--danger-soft);
	}

	[role='menuitem'][aria-disabled='true'] {
		opacity: 0.45;
		cursor: default;
		background: none;
	}

	.hint {
		margin-left: auto;
		padding-left: 16px;
		color: var(--faint);
		font-size: 12px;
	}

	.separator {
		height: 1px;
		margin: 4px 6px;
		background: var(--line);
	}

	@keyframes appear {
		from {
			opacity: 0;
		}
	}
</style>
