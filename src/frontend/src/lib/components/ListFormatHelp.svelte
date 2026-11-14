<script lang="ts">
	import Icon from './Icon.svelte';

	const rows: [string, string][] = [
		['example.com, domain:example.com', 'домен и поддомены'],
		['full:www.example.com', 'только этот домен (в Keenetic — как обычный)'],
		['91.108.4.0/22, 149.154.167.51', 'IPv4-подсеть или адрес'],
		['2001:67c:4e8::/48', 'IPv6-подсеть или адрес'],
		['include:games', 'вложить список из этого же источника'],
		['include:games @ads / @!ads', 'только строки с атрибутом / без него'],
		['ads.example.com @ads', 'строка с атрибутом для фильтра маршрута'],
		['keyword:…, regexp:…', 'пропускаются: Keenetic их не умеет'],
		['# комментарий', 'можно и в конце строки']
	];
</script>

<details class="help">
	<summary><Icon name="info" size={16} />Как устроены списки</summary>
	<div class="content">
		<p>
			Репозиторий — публичный на GitHub, списки лежат в папке <code>data/</code>: один файл — один
			список, имя файла — имя списка (латиница в нижнем регистре, цифры, <code>- . _ !</code>,
			расширение <code>.txt</code> отбрасывается). Вложенные папки не читаются.
		</p>
		<table>
			<tbody>
				{#each rows as [syntax, meaning] (syntax)}
					<tr><td><code>{syntax}</code></td><td>{meaning}</td></tr>
				{/each}
			</tbody>
		</table>
		<p>
			Домены, IPv4 и IPv6 можно смешивать: в Keenetic это одна группа <code>fg-&lt;список&gt;</code>
			по 1000 элементов, дальше части <code>-2</code>, <code>-3</code>. Кириллические домены пишутся
			в punycode (<code>xn--…</code>). Цикл include — ошибка списка.
		</p>
	</div>
</details>

<style>
	.help {
		border: 1px solid var(--line);
		border-radius: var(--r-md);
		background: var(--surface);
	}

	summary {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 12px 16px;
		color: var(--ink-2);
		font-weight: 500;
		cursor: pointer;
		list-style: none;
	}

	summary::-webkit-details-marker {
		display: none;
	}

	summary :global(.icon) {
		color: var(--accent-text);
	}

	.content {
		display: grid;
		gap: 12px;
		padding: 0 16px 16px;
		color: var(--ink-2);
		font-size: 13.5px;
	}

	p {
		max-width: 80ch;
	}

	table {
		border-collapse: collapse;
	}

	td {
		padding: 6px 16px 6px 0;
		border-top: 1px solid var(--line);
		vertical-align: top;
	}
</style>
