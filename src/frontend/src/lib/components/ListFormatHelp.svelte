<details class="help">
	<summary>Формат списков</summary>
	<p>
		Репозиторий: публичный на GitHub, списки в папке <code>data/</code> —
		<b>один файл = один список</b>, имя файла — имя списка (латиница в нижнем регистре, цифры,
		<code>- . _ !</code>, расширение
		<code>.txt</code> отбрасывается). Вложенные папки не читаются.
	</p>
	<table>
		<tbody>
			<tr
				><td><code>example.com</code>, <code>domain:example.com</code></td><td>домен и поддомены</td
				></tr
			>
			<tr><td><code>full:www.example.com</code></td><td>домен (в Keenetic — как обычный)</td></tr>
			<tr
				><td><code>91.108.4.0/22</code>, <code>149.154.167.51</code></td><td
					>IPv4-подсеть или адрес</td
				></tr
			>
			<tr><td><code>2001:67c:4e8::/48</code></td><td>IPv6-подсеть или адрес</td></tr>
			<tr><td><code>include:games</code></td><td>вложить список из этого же источника</td></tr>
			<tr>
				<td><code>include:games @ads</code> / <code>@!ads</code></td>
				<td>только строки с атрибутом / без него</td>
			</tr>
			<tr
				><td><code>ads.example.com @ads</code></td><td>строка с атрибутом для фильтра маршрута</td
				></tr
			>
			<tr
				><td><code>keyword:…</code>, <code>regexp:…</code></td><td
					>пропускаются: Keenetic их не умеет</td
				></tr
			>
			<tr><td><code># комментарий</code></td><td>можно и в конце строки</td></tr>
		</tbody>
	</table>
	<p>
		Домены, IPv4 и IPv6 можно смешивать: в Keenetic это одна группа <code>fg-&lt;список&gt;</code>
		(по 1000 элементов, дальше части <code>-2</code>, <code>-3</code>). Кириллические домены — в
		punycode (<code>xn--…</code>). Цикл include — ошибка списка.
	</p>
</details>

<style>
	.help {
		background: var(--surface);
		border: 1px solid var(--border);
		border-radius: var(--radius);
		padding: 8px 12px;
	}

	summary {
		cursor: pointer;
		color: var(--accent);
	}

	table {
		margin: 8px 0;
	}

	td {
		padding: 4px 8px;
		font-size: 0.92em;
	}

	p {
		margin: 8px 0;
		font-size: 0.92em;
	}
</style>
