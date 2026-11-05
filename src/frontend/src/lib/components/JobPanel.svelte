<script lang="ts">
	import { activeJobs, remove } from '#lib/jobs.svelte.js';
	import { formatDateTime } from '#lib/format.js';
</script>

{#if activeJobs.length > 0}
	<aside class="jobs">
		{#each activeJobs as job (job.id)}
			<section class="job {job.status.toLowerCase()}">
				<header>
					<strong>{job.title}</strong>
					<span class="status">
						{job.status === 'Running'
							? 'выполняется…'
							: job.status === 'Succeeded'
								? 'готово'
								: 'ошибка'}
					</span>
					{#if job.status !== 'Running'}
						<button class="close" aria-label="Скрыть" onclick={() => remove(job.id)}>×</button>
					{/if}
				</header>
				<ol>
					{#each job.steps.slice(-6) as step, index (index)}
						<li title={formatDateTime(step.at)}>{step.message}</li>
					{/each}
				</ol>
				{#if job.error}<p class="error">{job.error}</p>{/if}
			</section>
		{/each}
	</aside>
{/if}

<style>
	.jobs {
		position: fixed;
		left: 16px;
		bottom: 16px;
		display: flex;
		flex-direction: column;
		gap: 8px;
		width: min(420px, calc(100vw - 32px));
		z-index: 19;
	}

	.job {
		padding: 10px 12px;
		border-radius: var(--radius);
		background: var(--surface);
		border: 1px solid var(--border);
		border-left: 4px solid var(--accent);
		box-shadow: 0 4px 16px rgb(0 0 0 / 0.15);
	}

	.job.succeeded {
		border-left-color: var(--ok);
	}

	.job.failed {
		border-left-color: var(--danger);
	}

	header {
		display: flex;
		align-items: baseline;
		gap: 8px;
	}

	.status {
		color: var(--muted);
		margin-left: auto;
	}

	ol {
		margin: 6px 0 0;
		padding-left: 20px;
		color: var(--muted);
		font-size: 0.9em;
	}

	.error {
		margin: 6px 0 0;
	}

	.close {
		padding: 0 6px;
		border: none;
		background: transparent;
		color: var(--muted);
		font-size: 1.1rem;
	}
</style>
