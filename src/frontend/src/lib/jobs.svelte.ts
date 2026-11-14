import { unwrap } from './api/errors.js';
import { waitForJob, type Job } from './jobs.js';
import { notify, notifyError } from './toast.svelte.js';

/** Фоновые задачи, запущенные из этой вкладки; панель в layout показывает их ход. */
export const activeJobs = $state<Job[]>([]);

/**
 * Запускает фоновую задачу (ответ 202 + JobDto), показывает её ход и ждёт завершения.
 * Возвращает true, если задача завершилась успешно.
 */
export async function trackJob(
	start: Promise<{ data?: Job; error?: unknown; response: Response }>
): Promise<boolean> {
	let job: Job;
	try {
		job = await unwrap(start);
	} catch (error) {
		notifyError(error);
		return false;
	}

	return followJob(job);
}

/** Показывает ход уже запущенной задачи и ждёт её завершения. Возвращает true при успехе. */
export async function followJob(job: Job): Promise<boolean> {
	if (activeJobs.some((item) => item.id === job.id)) {
		try {
			await waitForJob(job);
			return true;
		} catch {
			return false;
		}
	}

	activeJobs.push(job);
	try {
		const done = await waitForJob(job, (current) => replace(current));
		notify('ok', `${done.title}: готово`);
		return true;
	} catch (error) {
		notifyError(error);
		return false;
	} finally {
		setTimeout(() => remove(job.id), 5000);
	}
}

export function remove(id: string): void {
	const index = activeJobs.findIndex((job) => job.id === id);
	if (index >= 0) activeJobs.splice(index, 1);
}

function replace(job: Job): void {
	const index = activeJobs.findIndex((item) => item.id === job.id);
	if (index >= 0) activeJobs[index] = job;
}
