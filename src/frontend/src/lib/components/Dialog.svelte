<script lang="ts">
	import type { Snippet } from 'svelte';
	import Icon from './Icon.svelte';

	interface Props {
		open: boolean;
		title: string;
		/** Подзаголовок под названием. */
		description?: string;
		/** `center` — модальное окно, `side` — панель справа (формы создания и правки). */
		variant?: 'center' | 'side';
		/** `xl` — мастер: на узком экране занимает весь экран. */
		size?: 'sm' | 'md' | 'lg' | 'xl';
		/** false — окно не закрывается ни подложкой, ни Esc, ни крестиком (идёт операция). */
		dismissible?: boolean;
		/** Тело без отступов и прокрутки: раскладку задаёт содержимое. */
		flush?: boolean;
		/** Ctrl+Enter / ⌘+Enter внутри диалога — основное действие. */
		onsubmit?: () => void;
		children: Snippet;
		actions?: Snippet;
	}

	let {
		open = $bindable(),
		title,
		description,
		variant = 'center',
		size = 'md',
		dismissible = true,
		flush = false,
		onsubmit,
		children,
		actions
	}: Props = $props();

	let dialog: HTMLDialogElement;
	const titleId = `dialog-${Math.random().toString(36).slice(2, 8)}`;

	$effect(() => {
		if (open && !dialog.open) dialog.showModal();
		if (!open && dialog.open) dialog.close();
	});

	function onkeydown(event: KeyboardEvent) {
		if (onsubmit && event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
			event.preventDefault();
			onsubmit();
		}
	}

	/** Клик по подложке закрывает окно. */
	function onclick(event: MouseEvent) {
		if (event.target === dialog && dismissible) open = false;
	}

	/** Esc при запрещённом закрытии гасится. */
	function oncancel(event: Event) {
		if (!dismissible) event.preventDefault();
	}

	/** Браузер может закрыть окно и при погашенном cancel (повторный Esc): тогда открываем снова. */
	function onclose() {
		if (!dismissible && open) dialog.showModal();
		else open = false;
	}
</script>

<dialog
	bind:this={dialog}
	class="{variant} {size}"
	aria-labelledby={titleId}
	{onclose}
	{oncancel}
	{onkeydown}
	{onclick}
>
	{#if open}
		<div class="frame">
			<header>
				<div class="titles">
					<h2 id={titleId}>{title}</h2>
					{#if description}<p class="muted">{description}</p>{/if}
				</div>
				<button
					class="btn ghost icon sm"
					aria-label="Закрыть"
					disabled={!dismissible}
					onclick={() => (open = false)}
				>
					<Icon name="close" />
				</button>
			</header>
			<div class="body" class:flush>{@render children()}</div>
			{#if actions}
				<footer>{@render actions()}</footer>
			{/if}
		</div>
	{/if}
</dialog>

<style>
	dialog {
		padding: 0;
		border: 1px solid var(--line);
		background: var(--surface);
		color: var(--ink);
		box-shadow: var(--shadow-pop);
		overflow: hidden;
	}

	dialog::backdrop {
		background: var(--backdrop);
	}

	.frame {
		display: flex;
		flex-direction: column;
		max-height: inherit;
		height: 100%;
	}

	/* Окно по центру */
	.center {
		width: min(520px, calc(100vw - 32px));
		max-height: calc(100dvh - 64px);
		border-radius: var(--r-lg);
	}

	.center.sm {
		width: min(420px, calc(100vw - 32px));
	}

	.center.lg {
		width: min(880px, calc(100vw - 32px));
	}

	.center.xl {
		width: min(960px, calc(100vw - 32px));
		height: min(680px, calc(100dvh - 64px));
	}

	.center[open] {
		animation: pop 0.16s var(--ease);
	}

	/* Панель справа */
	.side {
		margin: 0 0 0 auto;
		width: min(480px, 100vw);
		height: 100dvh;
		max-height: 100dvh;
		border-width: 0 0 0 1px;
		border-radius: 0;
	}

	.side.lg {
		width: min(720px, 100vw);
	}

	.side[open] {
		animation: slide 0.2s var(--ease);
	}

	header {
		display: flex;
		align-items: flex-start;
		gap: 12px;
		padding: 18px 20px 14px;
		border-bottom: 1px solid var(--line);
	}

	.titles {
		flex: 1;
		min-width: 0;
	}

	.titles p {
		margin-top: 2px;
		font-size: 13px;
	}

	.body {
		flex: 1;
		overflow: auto;
		padding: 20px;
	}

	.body.flush {
		display: flex;
		flex-direction: column;
		min-height: 0;
		padding: 0;
		overflow: hidden;
	}

	footer {
		display: flex;
		justify-content: flex-end;
		gap: 8px;
		padding: 14px 20px;
		border-top: 1px solid var(--line);
		background: var(--surface-2);
	}

	@media (max-width: 720px) {
		.center.xl {
			width: 100vw;
			max-width: 100vw;
			height: 100dvh;
			max-height: 100dvh;
			border: 0;
			border-radius: 0;
		}
	}

	@keyframes pop {
		from {
			opacity: 0;
			transform: translateY(6px) scale(0.98);
		}
	}

	@keyframes slide {
		from {
			transform: translateX(24px);
			opacity: 0;
		}
	}
</style>
