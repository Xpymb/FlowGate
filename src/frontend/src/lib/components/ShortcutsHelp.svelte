<script lang="ts">
	import Dialog from './Dialog.svelte';

	interface Props {
		open: boolean;
	}

	let { open = $bindable() }: Props = $props();

	const shortcuts: [string[], string][] = [
		[['Ctrl', 'K'], 'Поиск по разделам, объектам и командам'],
		[['/'], 'Поиск на странице'],
		[['N'], 'Добавление объекта'],
		[['Ctrl', 'S'], 'Сохранение формы'],
		[['Ctrl', 'Enter'], 'Подтверждение в диалоге'],
		[['Esc'], 'Закрытие окна, меню или сброс поиска'],
		[['?'], 'Справка по горячим клавишам']
	];
</script>

<Dialog bind:open title="Горячие клавиши" size="sm">
	<dl>
		{#each shortcuts as [keys, text] (text)}
			<dt>
				{#each keys as key (key)}<kbd>{key}</kbd>{/each}
			</dt>
			<dd>{text}</dd>
		{/each}
	</dl>
	<p class="muted small">В macOS вместо Ctrl используется ⌘.</p>
</Dialog>

<style>
	dl {
		display: grid;
		grid-template-columns: max-content 1fr;
		gap: 12px 20px;
		margin: 0 0 16px;
	}

	dt {
		display: flex;
		gap: 4px;
	}

	dd {
		margin: 0;
		color: var(--ink-2);
	}
</style>
