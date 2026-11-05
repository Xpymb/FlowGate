<script lang="ts">
	import { untrack } from 'svelte';
	import type { EditorView } from 'codemirror';

	interface Props {
		value: string;
		readonly?: boolean;
	}

	let { value = $bindable(), readonly = false }: Props = $props();
	let host = $state<HTMLDivElement>();
	let view = $state.raw<EditorView>();
	let failed = $state(false);

	$effect(() => {
		const parent = host;
		if (!parent) return;
		let disposed = false;
		void (async () => {
			try {
				const [{ EditorView, basicSetup }, { json }, { EditorState }, { oneDark }] =
					await Promise.all([
						import('codemirror'),
						import('@codemirror/lang-json'),
						import('@codemirror/state'),
						import('@codemirror/theme-one-dark')
					]);
				if (disposed) return;
				const dark = window.matchMedia('(prefers-color-scheme: dark)').matches;
				view = new EditorView({
					parent,
					doc: untrack(() => value),
					extensions: [
						basicSetup,
						json(),
						EditorState.readOnly.of(untrack(() => readonly)),
						EditorView.lineWrapping,
						...(dark ? [oneDark] : []),
						EditorView.updateListener.of((update) => {
							if (update.docChanged) value = update.state.doc.toString();
						})
					]
				});
			} catch {
				failed = true;
			}
		})();
		return () => {
			disposed = true;
			view?.destroy();
			view = undefined;
		};
	});

	$effect(() => {
		const next = value;
		const editor = view;
		if (!editor) return;
		const current = editor.state.doc.toString();
		if (current !== next) {
			editor.dispatch({ changes: { from: 0, to: current.length, insert: next } });
		}
	});
</script>

{#if failed}
	<textarea class="fallback" bind:value {readonly} spellcheck="false"></textarea>
{:else}
	<div class="editor" bind:this={host}></div>
{/if}

<style>
	.editor,
	.fallback {
		border: 1px solid var(--border);
		border-radius: var(--radius);
		overflow: hidden;
		min-height: 320px;
		max-height: 60vh;
		overflow-y: auto;
	}

	.editor :global(.cm-editor) {
		min-height: 320px;
	}

	.fallback {
		width: 100%;
		font-family: var(--mono);
		padding: 8px;
	}
</style>
