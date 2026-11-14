<script lang="ts">
	import Icon from './Icon.svelte';

	const rows: [string, string][] = [
		['example.com, domain:example.com', 'домен и поддомены'],
		['full:www.example.com', 'точный домен (в Keenetic обрабатывается как обычный)'],
		['91.108.4.0/22, 149.154.167.51', 'IPv4-подсеть или адрес'],
		['2001:67c:4e8::/48', 'IPv6-подсеть или адрес'],
		['include:games', 'включение списка из того же источника'],
		['include:games @ads / @!ads', 'только строки с атрибутом / без него'],
		['ads.example.com @ads', 'строка с атрибутом для фильтрации'],
		['keyword:…, regexp:…', 'не поддерживаются Keenetic, пропускаются'],
		['# комментарий', 'допускается в конце строки']
	];
</script>

<details class="help">
	<summary><Icon name="info" size={16} />Формат списков</summary>
	<div class="content">
		<p>
			Источник — публичный репозиторий GitHub. Списки размещаются в каталоге <code>data/</code>,
			один файл соответствует одному списку. Имя файла — имя списка (латиница в нижнем регистре,
			цифры, <code>- . _ !</code>, расширение <code>.txt</code> отбрасывается). Вложенные каталоги не
			обрабатываются.
		</p>
		<table>
			<tbody>
				{#each rows as [syntax, meaning] (syntax)}
					<tr><td><code>{syntax}</code></td><td>{meaning}</td></tr>
				{/each}
			</tbody>
		</table>
		<p>
			Домены, IPv4 и IPv6 допускаются в одном списке. В Keenetic создаётся группа <code
				>fg-&lt;список&gt;</code
			>
			до 1000 элементов, при превышении — дополнительные части <code>-2</code>, <code>-3</code>.
			Кириллические домены указываются в punycode (<code>xn--…</code>). Циклические include
			считаются ошибкой.
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
