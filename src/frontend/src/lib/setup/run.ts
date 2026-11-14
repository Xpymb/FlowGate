import { api } from '../api/client';
import { ApiError, unwrap } from '../api/errors';
import type { components } from '../api/schema';
import { suggestConnectionName } from '../connections';
import { toNumber } from '../format';
import { findRunningJob, waitForJob, type Job } from '../jobs';
import { listKey } from './presets';
import type { RunProgress, RunStep, RunStepId, SelectedList, SetupDraft } from './setup.svelte';

type Server = components['schemas']['ServerDto'];
type SettingsSaveRequest = components['schemas']['SettingsSaveRequest'];

const applyJobKind = 'dns-routes-apply';

/** Что шаг сообщает по завершении. */
interface StepResult {
	skipped?: boolean;
	detail?: string;
}

interface Context {
	draft: SetupDraft;
	lists: SelectedList[];
	progress: RunProgress;
	step: RunStep;
}

/** Ошибка шага, понятная пользователю. */
class SetupError extends Error {}

/**
 * Выполняет настройку по черновику мастера. Готовые шаги пропускаются, поэтому повторный вызов после ошибки
 * продолжает с упавшего шага.
 * @returns true, если все шаги прошли
 */
export async function runSetup(
	draft: SetupDraft,
	lists: SelectedList[],
	progress: RunProgress
): Promise<boolean> {
	progress.phase = 'running';
	for (const step of progress.steps) {
		if (step.status === 'done' || step.status === 'skipped') continue;
		step.status = 'running';
		step.error = undefined;
		step.detail = '';
		try {
			const result = await handlers[step.id]({ draft, lists, progress, step });
			step.status = result.skipped ? 'skipped' : 'done';
			step.detail = result.detail ?? '';
		} catch (error) {
			step.status = 'failed';
			step.error = describe(step.id, error);
			progress.phase = 'failed';
			return false;
		}
	}
	progress.phase = 'done';
	return true;
}

const handlers: Record<RunStepId, (context: Context) => Promise<StepResult>> = {
	xray: installXray,
	servers: addServers,
	proxy: enableUpdateProxy,
	connection: createConnections,
	lists: downloadLists,
	routes: createRoutes,
	apply: applyRoutes
};

async function installXray({ step }: Context): Promise<StepResult> {
	const status = await unwrap(api.GET('/api/xray'));
	if (status.installed)
		return { skipped: true, detail: `Уже установлен, ${status.version ?? ''}`.trim() };

	const job = await unwrap(
		api.POST('/api/xray/install', { params: { query: { via: 'Direct' } }, body: { version: null } })
	);
	await follow(job, step);
	const installed = await unwrap(api.GET('/api/xray'));
	return { detail: installed.version ? `Версия ${installed.version}` : 'Установлен' };
}

async function addServers({ draft, progress, step }: Context): Promise<StepResult> {
	let subscriptionId: string | undefined;
	if (draft.serverMode === 'subscription') {
		subscriptionId = progress.subscriptionId;
		if (subscriptionId) {
			step.detail = 'Повторная загрузка подписки';
			await follow(
				await unwrap(
					api.POST('/api/subscriptions/{id}/refresh', {
						params: { path: { id: subscriptionId }, query: { via: 'Auto' } }
					})
				),
				step
			);
		} else {
			const created = await unwrap(
				api.POST('/api/subscriptions', {
					body: {
						url: draft.subscriptionUrl.trim(),
						autoUpdate: true,
						sendHwid: false,
						sendDeviceInfo: false
					}
				})
			);
			subscriptionId = created.id;
			progress.subscriptionId = created.id;
			if (created.refreshJobId) {
				const job = await unwrap(
					api.GET('/api/jobs/{id}', { params: { path: { id: created.refreshJobId } } })
				);
				await follow(job, step);
			}
		}
	} else if (draft.serverMode === 'links') {
		const result = await unwrap(
			api.POST('/api/servers/import', { body: { text: draft.linksText } })
		);
		if (result.rejected.length > 0)
			progress.warnings.push(`Не добавлено ссылок с ошибками: ${result.rejected.length}`);
	}

	const servers = await unwrap(api.GET('/api/servers'));
	const names: string[] = [];
	for (const connection of draft.connections) {
		const server =
			draft.serverMode === 'existing'
				? servers.find((item) => item.id === connection.serverRef)
				: pickServer(servers, connection.serverRef, subscriptionId);
		if (!server)
			throw new SetupError(
				draft.serverMode === 'existing'
					? 'Выбранный сервер больше не существует. Вернитесь к шагу «Серверы».'
					: 'Выбранного сервера нет среди загруженных: провайдер мог обновить список. Вернитесь к шагу «Серверы» и выберите другой.'
			);
		progress.serverIds[connection.uid] = server.id;
		if (!names.includes(server.name)) names.push(server.name);
	}
	return {
		skipped: draft.serverMode === 'existing',
		detail: names.length === 1 ? `Сервер «${names[0]}»` : `Серверы: ${names.join(', ')}`
	};
}

/** Сохранённый сервер по ключу идентичности из предпросмотра; серверы этой подписки — в приоритете. */
function pickServer(servers: Server[], key: string, subscriptionId?: string): Server | undefined {
	const matches = servers.filter((server) => server.importKey === key);
	return (
		matches.find((server) =>
			subscriptionId ? server.subscriptionId === subscriptionId : server.source !== 'Subscription'
		) ?? matches[0]
	);
}

async function enableUpdateProxy({ draft, progress }: Context): Promise<StepResult> {
	const serverId = progress.serverIds[draft.connections[0]?.uid ?? ''];
	if (!draft.useUpdateProxy || !serverId) return { skipped: true, detail: 'Не включён' };

	const current = await unwrap(api.GET('/api/settings'));
	const hours = (value: number | string | null) => toNumber(value) ?? null;
	const body: SettingsSaveRequest = {
		xrayLogLevel: current.xrayLogLevel,
		updateProxy: {
			enabled: true,
			serverId,
			extraDomains: current.updateProxy.extraDomains
		},
		intervals: {
			subscriptionDefaultHours: hours(current.intervals.subscriptionDefault.hours),
			domainSourceRefreshHours: hours(current.intervals.domainSourceRefresh.hours),
			xrayUpdateCheckHours: hours(current.intervals.xrayUpdateCheck.hours),
			systemUpdateCheckHours: hours(current.intervals.systemUpdateCheck.hours)
		}
	};
	await unwrap(api.PUT('/api/settings', { body }));
	return { detail: 'GitHub и подписки загружаются через сервер первого соединения' };
}

async function createConnections({ draft, progress }: Context): Promise<StepResult> {
	const existing = await unwrap(api.GET('/api/connections'));
	const taken = existing.map((item) => item.name);
	const claimed = Object.values(progress.connections).map((item) => item.id);
	const parts: string[] = [];
	let created = 0;

	for (const connection of draft.connections) {
		const serverId = progress.serverIds[connection.uid];
		if (!serverId) throw new SetupError('Сервер соединения не выбран');
		const done = progress.connections[connection.uid];
		if (done && done.serverId === serverId && done.kind === connection.kind) {
			parts.push(done.name);
			continue;
		}

		const same = existing.find(
			(item) =>
				item.serverId === serverId && item.kind === connection.kind && !claimed.includes(item.id)
		);
		if (same) {
			progress.connections[connection.uid] = {
				id: same.id,
				name: same.name,
				serverId,
				kind: connection.kind
			};
			claimed.push(same.id);
			parts.push(`${same.name} (уже было)`);
			continue;
		}

		const name = suggestConnectionName(connection.kind, taken);
		const result = await unwrap(
			api.POST('/api/connections', { body: { name, kind: connection.kind, serverId } })
		);
		progress.connections[connection.uid] = {
			id: result.id,
			name: result.name,
			serverId,
			kind: connection.kind
		};
		claimed.push(result.id);
		taken.push(result.name);
		parts.push(`${result.name} → ${result.interfaceName}`);
		created++;
	}
	return { skipped: created === 0, detail: parts.join(', ') };
}

async function downloadLists({ lists, progress, step }: Context): Promise<StepResult> {
	const sources = await unwrap(api.GET('/api/domain-sources'));
	for (const sourceId of new Set(lists.map((list) => list.sourceId))) {
		const source = sources.find((item) => item.id === sourceId);
		if (!source) throw new SetupError(`Источник «${sourceId}» не найден`);
		if (source.downloaded) continue;
		step.detail = `Загрузка «${source.name}»`;
		await follow(
			await unwrap(
				api.POST('/api/domain-sources/{id}/refresh', {
					params: { path: { id: sourceId }, query: { via: 'Auto' } }
				})
			),
			step
		);
	}

	progress.missingLists = [];
	for (const list of lists) {
		const found = await unwrap(
			api.GET('/api/domain-sources/{id}/lists', {
				params: { path: { id: list.sourceId }, query: { query: list.name } }
			})
		);
		if (!found.some((item) => item.name === list.name)) {
			progress.missingLists.push(listKey(list));
			progress.warnings.push(`Списка «${list.name}» нет в источнике: маршрут не создан`);
		}
	}
	const ready = lists.length - progress.missingLists.length;
	if (ready === 0) throw new SetupError('Ни одного выбранного списка нет в источниках');
	return { detail: `Готово списков: ${ready}` };
}

async function createRoutes({ lists, progress }: Context): Promise<StepResult> {
	let created = 0;
	for (const list of lists) {
		const key = listKey(list);
		if (progress.missingLists?.includes(key)) continue;
		const connectionId = progress.connections[list.connectionUid]?.id;
		if (!connectionId) throw new SetupError(`Для списка «${list.name}» не выбрано соединение`);
		const known = progress.routes[key];
		if (known) {
			if (known.connectionId !== connectionId) {
				await unwrap(
					api.PUT('/api/dns-routes/{id}', {
						params: { path: { id: known.id } },
						body: { connectionId, attributeFilter: null, exclusive: false }
					})
				);
				known.connectionId = connectionId;
			}
			continue;
		}
		try {
			const route = await unwrap(
				api.POST('/api/dns-routes', {
					body: {
						sourceId: list.sourceId,
						listName: list.name,
						connectionId,
						attributeFilter: null,
						exclusive: false
					}
				})
			);
			progress.routes[key] = { id: route.id, connectionId };
			created++;
		} catch (error) {
			if (!(error instanceof ApiError) || error.status !== 409) throw error;
			const routes = await unwrap(api.GET('/api/dns-routes'));
			const existing = routes.find(
				(route) =>
					route.sourceId === list.sourceId && route.listName === list.name && !route.attributeFilter
			);
			if (!existing) throw error;
			progress.routes[key] = { id: existing.id, connectionId: existing.connectionId };
			if (existing.connectionId !== connectionId)
				progress.warnings.push(
					`Маршрут «${list.name}» уже был и идёт через «${existing.connectionName ?? 'другое соединение'}»: оставлен как есть`
				);
		}
	}
	const total = Object.keys(progress.routes).length;
	return {
		detail:
			created === total
				? `Создано: ${created}`
				: `Создано: ${created}, уже были: ${total - created}`
	};
}

async function applyRoutes({ progress, step }: Context): Promise<StepResult> {
	await settleApply(step);

	const ids = new Set(Object.values(progress.routes).map((route) => route.id));
	let ours = (await unwrap(api.GET('/api/dns-routes'))).filter((route) => ids.has(route.id));
	if (ours.some((route) => !route.lastApply || route.lastApply.status === 'Waiting')) {
		try {
			await follow(await unwrap(api.POST('/api/dns-routes/apply')), step);
		} catch (error) {
			if (!(error instanceof ApiError) || error.status !== 409) throw error;
		}
		await settleApply(step);
		ours = (await unwrap(api.GET('/api/dns-routes'))).filter((route) => ids.has(route.id));
	}

	const failed = ours.filter((route) => route.lastApply?.status === 'Failed');
	if (failed.length > 0)
		throw new SetupError(
			`Keenetic не принял маршруты: ${failed.map((route) => `${route.listName} — ${route.lastApply?.error ?? 'без причины'}`).join('; ')}`
		);
	return { detail: `Применено маршрутов: ${ours.length}` };
}

/** Ждёт, пока фоновое применение маршрутов, запущенное их созданием, не закончится. */
async function settleApply(step: RunStep) {
	for (let job = await findRunningJob(applyJobKind); job; job = await findRunningJob(applyJobKind))
		await follow(job, step);
}

/** Ждёт фоновую задачу, показывая её последний шаг в строке шага мастера. */
async function follow(job: Job, step: RunStep): Promise<Job> {
	return waitForJob(job, (current) => {
		const last = current.steps.at(-1);
		if (last) step.detail = last.message;
	});
}

/** Текст ошибки шага с подсказкой, что делать. */
function describe(id: RunStepId, error: unknown): string {
	const message = error instanceof Error ? error.message : String(error);
	if (error instanceof SetupError) return message;
	if (id === 'xray')
		return `${message}. Xray скачивается с GitHub напрямую: проверьте, что роутер выходит в интернет и GitHub доступен.`;
	if (id === 'lists')
		return `${message}. Проверьте, что GitHub доступен напрямую или через прокси обновлений.`;
	return message;
}
