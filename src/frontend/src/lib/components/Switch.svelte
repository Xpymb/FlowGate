<script lang="ts">
	interface Props {
		checked: boolean;
		label: string;
		disabled?: boolean;
		busy?: boolean;
		title?: string;
		onchange: () => void;
	}

	let { checked, label, disabled = false, busy = false, title, onchange }: Props = $props();
</script>

<button
	class="switch"
	class:busy
	role="switch"
	aria-checked={checked}
	aria-label={label}
	aria-busy={busy}
	disabled={disabled || busy}
	{title}
	onclick={(event) => {
		event.stopPropagation();
		onchange();
	}}
>
	<span class="thumb"></span>
</button>

<style>
	.switch {
		position: relative;
		flex: none;
		width: 34px;
		height: 20px;
		border-radius: 999px;
		background: var(--line-strong);
		transition: background-color 0.15s;
	}

	.switch[aria-checked='true'] {
		background: var(--accent);
	}

	.thumb {
		position: absolute;
		top: 2px;
		left: 2px;
		width: 16px;
		height: 16px;
		border-radius: 999px;
		background: #fff;
		box-shadow: 0 1px 2px rgb(0 0 0 / 0.25);
		transition: transform 0.15s var(--ease);
	}

	.switch[aria-checked='true'] .thumb {
		transform: translateX(14px);
	}

	.switch:disabled {
		cursor: default;
		opacity: 0.5;
	}

	.switch.busy {
		opacity: 1;
	}

	.switch.busy .thumb {
		animation: breathe 0.9s ease-in-out infinite;
	}

	@keyframes breathe {
		50% {
			opacity: 0.5;
		}
	}
</style>
