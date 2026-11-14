<script lang="ts">
	import Icon from './Icon.svelte';

	/** Пример репозитория: строка дерева и пояснение к ней. */
	const tree: [string, string][] = [
		['my-lists/', 'ваш публичный репозиторий'],
		['├── README.md', 'файлы вне data/ не читаются'],
		['└── data/', 'каталог со списками'],
		['    ├── telegram', 'список «telegram»'],
		['    ├── work.txt', 'список «work», .txt отбрасывается'],
		['    └── games', 'список «games»']
	];

	const example = `# Telegram
telegram.org
t.me
91.108.4.0/22
2001:67c:4e8::/48`;

	const syntax: [string, string][] = [
		['example.com', 'домен вместе с поддоменами'],
		['full:www.example.com', 'только этот домен'],
		['91.108.4.0/22', 'IPv4 или IPv6: адрес или подсеть'],
		['include:games', 'добавить строки другого списка из того же источника'],
		['ads.example.com @ads', 'строка с меткой — её можно отобрать в маршруте'],
		['# комментарий', 'игнорируется, можно и в конце строки']
	];
</script>

<details class="help">
	<summary><Icon name="info" size={16} />Как подготовить свои списки</summary>
	<div class="content">
		<section>
			<h3>Свой репозиторий на GitHub</h3>
			<p>
				Создайте публичный репозиторий, положите списки в каталог <code>data/</code> — один файл на
				список, имя файла становится именем списка. Затем добавьте источник «GitHub-репозиторий» с
				адресом репозитория. Формат совместим с
				<code>v2fly/domain-list-community</code>, поэтому его можно взять за основу.
			</p>
			<div class="example">
				<div class="tree" aria-label="Пример структуры репозитория">
					{#each tree as [line, note] (line)}
						<div class="tree-row">
							<code>{line}</code>
							<span>{note}</span>
						</div>
					{/each}
				</div>
				<div class="file">
					<div class="file-name"><Icon name="log" size={14} /><code>data/telegram</code></div>
					<pre>{example}</pre>
				</div>
			</div>
			<p class="muted">
				Имена списков — латиница в нижнем регистре, цифры и <code>- . _ !</code>. Вложенные каталоги
				внутри <code>data/</code> не читаются.
			</p>
		</section>

		<section>
			<h3>Без репозитория</h3>
			<p>
				Добавьте источник «Локальные списки» — списки создаются и редактируются прямо в FlowGate.
				Формат строк тот же.
			</p>
		</section>

		<section>
			<h3>Что писать в файле</h3>
			<table>
				<tbody>
					{#each syntax as [line, meaning] (line)}
						<tr><td><code>{line}</code></td><td>{meaning}</td></tr>
					{/each}
				</tbody>
			</table>
			<ul class="notes muted">
				<li>
					Строки <code>keyword:</code> и <code>regexp:</code> Keenetic не поддерживает — они пропускаются.
				</li>
				<li>Кириллические домены указываются в punycode: <code>xn--…</code>.</li>
				<li>Список больше 1000 элементов автоматически делится на несколько групп Keenetic.</li>
			</ul>
		</section>
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
		gap: 24px;
		padding: 4px 16px 20px;
		color: var(--ink-2);
		font-size: 13.5px;
	}

	section {
		display: grid;
		gap: 10px;
		min-width: 0;
	}

	h3 {
		color: var(--ink);
	}

	.example {
		display: grid;
		grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
		gap: 12px;
	}

	.example > * {
		min-width: 0;
	}

	.tree,
	.file {
		overflow-x: auto;
		border: 1px solid var(--line);
		border-radius: var(--r-sm);
		background: var(--surface-2);
	}

	.tree {
		display: grid;
		padding: 12px 14px;
	}

	.tree-row {
		display: grid;
		grid-template-columns: 15ch 1fr;
		gap: 16px;
		line-height: 1.75;
	}

	.tree-row code {
		white-space: pre;
		color: var(--ink);
	}

	.tree-row span {
		color: var(--faint);
		font-size: 12.5px;
	}

	.file-name {
		display: flex;
		align-items: center;
		gap: 6px;
		padding: 8px 12px;
		border-bottom: 1px solid var(--line);
		color: var(--muted);
	}

	.file pre {
		margin: 0;
		padding: 10px 12px;
		line-height: 1.65;
	}

	table {
		border-collapse: collapse;
	}

	td {
		overflow-wrap: anywhere;
		padding: 7px 20px 7px 0;
		border-top: 1px solid var(--line);
		vertical-align: top;
	}

	tr:first-child td {
		border-top: 0;
	}

	td:first-child {
		white-space: nowrap;
	}

	.notes {
		display: grid;
		gap: 4px;
		margin: 4px 0 0;
		padding-left: 18px;
	}

	@media (max-width: 720px) {
		.example {
			grid-template-columns: 1fr;
		}

		.tree-row {
			grid-template-columns: 1fr;
			gap: 0;
			margin-bottom: 4px;
		}

		td:first-child {
			white-space: normal;
		}
	}
</style>
