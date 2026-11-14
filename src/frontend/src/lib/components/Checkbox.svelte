<script lang="ts">
	interface Props {
		checked: boolean;
		indeterminate?: boolean;
		label: string;
		onchange: () => void;
	}

	let { checked, indeterminate = false, label, onchange }: Props = $props();
</script>

<input
	type="checkbox"
	aria-label={label}
	{checked}
	{indeterminate}
	onclick={(event) => event.stopPropagation()}
	{onchange}
/>
