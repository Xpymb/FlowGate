import { api } from './api/client';
import { ApiError, unwrap } from './api/errors';
import type { components } from './api/schema';

export type Job = components['schemas']['JobDto'];

const pollIntervalMs = 1000;

/** Задача ещё выполняется. */
export function isRunning(job: Job): boolean {
	return job.status === 'Running';
}

/**
 * Опрашивает фоновую задачу (ответ 202 + JobDto) до завершения.
 * Бросает {@link ApiError}, если задача завершилась ошибкой.
 */
export async function waitForJob(
	job: Job,
	onUpdate?: (job: Job) => void,
	signal?: AbortSignal
): Promise<Job> {
	let current = job;
	onUpdate?.(current);

	while (isRunning(current)) {
		await delay(pollIntervalMs, signal);
		current = await unwrap(
			api.GET('/api/jobs/{id}', { params: { path: { id: current.id } }, signal })
		);
		onUpdate?.(current);
	}

	if (current.status === 'Failed') {
		throw new ApiError(500, {
			errorCode: 'JOB_FAILED',
			message: current.error ?? `Задача «${current.title}» завершилась ошибкой`,
			traceId: current.id
		});
	}

	return current;
}

function delay(ms: number, signal?: AbortSignal): Promise<void> {
	return new Promise((resolve, reject) => {
		if (signal?.aborted) {
			reject(signal.reason);
			return;
		}
		const timer = setTimeout(resolve, ms);
		signal?.addEventListener(
			'abort',
			() => {
				clearTimeout(timer);
				reject(signal.reason);
			},
			{ once: true }
		);
	});
}
