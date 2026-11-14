<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		label: string;
		hint?: string;
		/** Ошибка поля: подсвечивает и заменяет подсказку. */
		error?: string;
		/** `stacked` — подпись над полем (узкие панели), `split` — подпись слева (страницы настроек). */
		layout?: 'stacked' | 'split';
		/** Подпись не оборачивает поле (группа радиокнопок, редактор). */
		group?: boolean;
		/** Обязательное поле: звёздочка у подписи. */
		required?: boolean;
		children: Snippet;
		extra?: Snippet;
	}

	let {
		label,
		hint,
		error,
		layout = 'stacked',
		group = false,
		required = false,
		children,
		extra
	}: Props = $props();
</script>

<svelte:element
	this={group ? 'div' : 'label'}
	class="field {layout}"
	class:invalid={!!error}
	role={group ? 'group' : undefined}
	aria-label={group ? label : undefined}
>
	<span class="caption">
		<span class="name"
			>{label}{#if required}<span class="required" aria-hidden="true">*</span>{/if}</span
		>
		{#if layout === 'split' && (error || hint)}
			<span class="hint" class:error={!!error}>{error ?? hint}</span>
		{/if}
	</span>
	<span class="control">
		{@render children()}
		{#if layout === 'stacked' && (error || hint)}
			<span class="hint" class:error={!!error}>{error ?? hint}</span>
		{/if}
		{#if extra}{@render extra()}{/if}
	</span>
</svelte:element>

<style>
	.field {
		display: grid;
		gap: 6px;
	}

	.split {
		grid-template-columns: minmax(0, 1fr);
		gap: 6px 32px;
		padding: 18px 0;
		border-top: 1px solid var(--line);
	}

	.split:first-child {
		border-top: 0;
		padding-top: 0;
	}

	@media (min-width: 900px) {
		.split {
			grid-template-columns: minmax(200px, 2fr) minmax(0, 3fr);
		}
	}

	.caption {
		display: grid;
		gap: 2px;
		align-content: start;
	}

	.name {
		font-weight: 500;
		color: var(--ink-2);
	}

	.stacked .name {
		font-size: 13px;
	}

	.hint.error {
		color: var(--danger);
	}

	.required {
		margin-left: 3px;
		color: var(--danger);
	}

	.invalid :global(input:focus),
	.invalid :global(select:focus),
	.invalid :global(textarea:focus) {
		box-shadow: 0 0 0 3px var(--danger-soft);
	}

	.control {
		display: grid;
		gap: 6px;
		min-width: 0;
	}

	.hint {
		color: var(--muted);
		font-size: 12.5px;
		line-height: 1.45;
	}

	.invalid :global(input),
	.invalid :global(select),
	.invalid :global(textarea) {
		border-color: var(--danger);
	}
</style>
