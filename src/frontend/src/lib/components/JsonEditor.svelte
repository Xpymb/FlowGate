<script lang="ts">
	import { untrack } from 'svelte';
	import type { Compartment } from '@codemirror/state';
	import type { EditorView } from 'codemirror';
	import { theme } from '#lib/theme.svelte.js';

	interface Props {
		value: string;
		readonly?: boolean;
		label?: string;
	}

	let { value = $bindable(), readonly = false, label = 'JSON' }: Props = $props();
	let host = $state<HTMLDivElement>();
	let view = $state.raw<EditorView>();
	let themeSlot = $state.raw<Compartment>();
	let darkTheme = $state.raw<unknown>();
	let failed = $state(false);

	$effect(() => {
		const parent = host;
		if (!parent) return;
		let disposed = false;
		void (async () => {
			try {
				const [{ EditorView, basicSetup }, { json }, { EditorState, Compartment }, { oneDark }] =
					await Promise.all([
						import('codemirror'),
						import('@codemirror/lang-json'),
						import('@codemirror/state'),
						import('@codemirror/theme-one-dark')
					]);
				if (disposed) return;
				const slot = new Compartment();
				darkTheme = oneDark;
				themeSlot = slot;
				view = new EditorView({
					parent,
					doc: untrack(() => value),
					extensions: [
						basicSetup,
						json(),
						EditorState.readOnly.of(untrack(() => readonly)),
						EditorView.lineWrapping,
						EditorView.contentAttributes.of({ 'aria-label': untrack(() => label) }),
						slot.of(untrack(() => theme.dark) ? oneDark : []),
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

	// Тема меняется на лету вместе с интерфейсом.
	$effect(() => {
		const dark = theme.dark;
		if (!view || !themeSlot) return;
		view.dispatch({
			effects: themeSlot.reconfigure(dark ? (darkTheme as Parameters<Compartment['of']>[0]) : [])
		});
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
	<textarea class="fallback" bind:value {readonly} spellcheck="false" aria-label={label}></textarea>
{:else}
	<div class="editor" class:readonly bind:this={host}></div>
{/if}

<style>
	.editor,
	.fallback {
		min-height: 320px;
		max-height: 62vh;
		overflow: auto;
		border: 1px solid var(--line-strong);
		border-radius: var(--r-sm);
		background: var(--surface);
	}

	.editor:focus-within {
		border-color: var(--accent);
		box-shadow: 0 0 0 3px var(--accent-soft);
	}

	.editor.readonly {
		background: var(--surface-2);
	}

	.editor :global(.cm-editor) {
		min-height: 320px;
		font-size: 13px;
	}

	.editor :global(.cm-editor.cm-focused) {
		outline: none;
	}

	.editor :global(.cm-scroller) {
		font-family: var(--mono);
	}

	.editor :global(.cm-gutters) {
		border-right-color: var(--line);
		background: var(--surface-2);
		color: var(--faint);
	}

	.fallback {
		width: 100%;
	}
</style>
