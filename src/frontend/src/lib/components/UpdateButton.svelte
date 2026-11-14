<script lang="ts">
	import type { components } from '#lib/api/schema.js';
	import Icon from './Icon.svelte';
	import Menu from './Menu.svelte';

	type UpdateVia = components['schemas']['UpdateVia'];

	interface Props {
		/** Текст основной кнопки: обновление по настройкам. */
		label: string;
		/** Идёт обновление: кнопка показывает ход и заблокирована. */
		busy?: boolean;
		disabled?: boolean;
		small?: boolean;
		primary?: boolean;
		onrun: (via: UpdateVia) => void;
	}

	let {
		label,
		busy = false,
		disabled = false,
		small = false,
		primary = false,
		onrun
	}: Props = $props();
</script>

<span class="split" class:primary>
	<button
		class="btn main"
		class:sm={small}
		class:primary
		{disabled}
		aria-busy={busy}
		title="По настройкам: через прокси обновлений, если он включён"
		onclick={() => onrun('Auto')}
	>
		<Icon name="refresh" size={small ? 14 : 16} />
		{label}
	</button>
	<Menu
		{small}
		ariaLabel="Как загружать"
		icon="chevronDown"
		buttonClass="toggle {primary ? 'primary' : ''}"
		disabled={busy || disabled}
		items={[
			{ label: 'Принудительно через прокси', icon: 'lock', onclick: () => onrun('Proxy') },
			{ label: 'Принудительно без прокси', icon: 'arrowRight', onclick: () => onrun('Direct') }
		]}
	/>
</span>

<style>
	.split {
		display: inline-flex;
	}

	.split :global(.main) {
		border-top-right-radius: 0;
		border-bottom-right-radius: 0;
	}

	.split :global(.toggle) {
		margin-left: -1px;
		border-top-left-radius: 0;
		border-bottom-left-radius: 0;
	}

	.split.primary :global(.toggle) {
		border-left-color: color-mix(in srgb, var(--accent-ink) 30%, var(--accent));
	}
</style>
