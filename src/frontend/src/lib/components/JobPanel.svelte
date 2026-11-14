<script lang="ts">
	import { activeJobs, remove } from '#lib/jobs.svelte.js';
	import { formatDateTime } from '#lib/format.js';
	import Icon from './Icon.svelte';

	let collapsed = $state<Record<string, boolean>>({});
</script>

{#if activeJobs.length > 0}
	<aside class="jobs" aria-label="Фоновые задачи" aria-live="polite">
		{#each activeJobs as job (job.id)}
			{@const running = job.status === 'Running'}
			{@const last = job.steps.at(-1)}
			<section class="job {job.status.toLowerCase()}">
				<header>
					<span class="indicator" aria-hidden="true">
						{#if running}<span class="spinner"></span>
						{:else if job.status === 'Succeeded'}<Icon name="check" size={14} />
						{:else}<Icon name="alert" size={14} />{/if}
					</span>
					<div class="title">
						<strong>{job.title}</strong>
						<span class="status">
							{running ? 'выполняется' : job.status === 'Succeeded' ? 'готово' : 'ошибка'}
						</span>
					</div>
					<button
						class="btn ghost sm icon"
						aria-label={collapsed[job.id] ? 'Показать шаги' : 'Скрыть шаги'}
						aria-expanded={!collapsed[job.id]}
						onclick={() => (collapsed[job.id] = !collapsed[job.id])}
					>
						<Icon name={collapsed[job.id] ? 'chevronRight' : 'chevronDown'} size={15} />
					</button>
					{#if !running}
						<button class="btn ghost sm icon" aria-label="Скрыть" onclick={() => remove(job.id)}>
							<Icon name="close" size={15} />
						</button>
					{/if}
				</header>
				{#if !collapsed[job.id]}
					<ol>
						{#each job.steps.slice(-5) as step, index (index)}
							<li class:current={step === last && running} title={formatDateTime(step.at)}>
								{step.message}
							</li>
						{/each}
					</ol>
				{/if}
				{#if job.error}<p class="error-text">{job.error}</p>{/if}
				{#if running}<div class="progress"></div>{/if}
			</section>
		{/each}
	</aside>
{/if}

<style>
	.jobs {
		position: fixed;
		right: 16px;
		bottom: 16px;
		z-index: 40;
		display: flex;
		flex-direction: column;
		gap: 8px;
		width: min(380px, calc(100vw - 32px));
	}

	.job {
		position: relative;
		overflow: hidden;
		padding: 10px 8px 12px 12px;
		border: 1px solid var(--line);
		border-radius: var(--r-md);
		background: var(--surface);
		box-shadow: var(--shadow-pop);
	}

	header {
		display: flex;
		align-items: center;
		gap: 10px;
	}

	.indicator {
		display: grid;
		place-items: center;
		width: 24px;
		height: 24px;
		border-radius: 6px;
		background: var(--accent-soft);
		color: var(--accent-text);
	}

	.succeeded .indicator {
		background: var(--ok-soft);
		color: var(--ok);
	}

	.failed .indicator {
		background: var(--danger-soft);
		color: var(--danger);
	}

	.spinner {
		width: 12px;
		height: 12px;
		border: 2px solid currentColor;
		border-right-color: transparent;
		border-radius: 50%;
		animation: spin 0.8s linear infinite;
	}

	.title {
		display: grid;
		flex: 1;
		min-width: 0;
		line-height: 1.3;
	}

	.status {
		color: var(--muted);
		font-size: 12.5px;
	}

	ol {
		display: grid;
		gap: 3px;
		margin: 10px 0 0 34px;
		padding: 0;
		list-style: none;
		color: var(--muted);
		font-size: 12.5px;
	}

	li.current {
		color: var(--ink);
	}

	.error-text {
		margin: 8px 0 0 34px;
		font-size: 13px;
	}

	.progress {
		position: absolute;
		left: 0;
		right: 0;
		bottom: 0;
		height: 2px;
		background: linear-gradient(90deg, transparent, var(--accent), transparent);
		background-size: 40% 100%;
		background-repeat: no-repeat;
		animation: sweep 1.3s linear infinite;
	}

	@keyframes sweep {
		from {
			background-position: -40% 0;
		}
		to {
			background-position: 140% 0;
		}
	}
</style>
