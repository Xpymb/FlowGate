import { hostOf } from './format.js';
import type { components } from './api/schema';

type Source = components['schemas']['DomainSourceDto'];
export type SourceSaveRequest = components['schemas']['DomainSourceSaveRequest'];

/** Откуда источник берёт данные — одной строкой. */
export function sourceOrigin(source: Source): string {
	switch (source.kind) {
		case 'GitHubRepository':
			return `${source.repository ?? ''}${source.branch ? ` (${source.branch})` : ''}`;
		case 'GeositeDat':
		case 'GeoipDat':
			return source.releaseRepository
				? `${source.releaseRepository} / ${source.asset ?? ''}`
				: source.url
					? hostOf(source.url)
					: '—';
		default:
			return 'правится в FlowGate';
	}
}

/** Форма источника: все поля строками, режим .dat — из релиза или по ссылке. */
export interface SourceForm {
	name: string;
	kind: string;
	repository: string;
	branch: string;
	datMode: 'release' | 'url';
	releaseRepository: string;
	asset: string;
	checksumAsset: string;
	url: string;
	checksumUrl: string;
	autoUpdate: boolean;
}

export function emptySourceForm(): SourceForm {
	return {
		name: '',
		kind: 'GitHubRepository',
		repository: '',
		branch: '',
		datMode: 'release',
		releaseRepository: '',
		asset: '',
		checksumAsset: '',
		url: '',
		checksumUrl: '',
		autoUpdate: true
	};
}

export function sourceToForm(source: Source): SourceForm {
	return {
		name: source.name,
		kind: source.kind,
		repository: source.repository ?? '',
		branch: source.branch ?? '',
		datMode: source.url ? 'url' : 'release',
		releaseRepository: source.releaseRepository ?? '',
		asset: source.asset ?? '',
		checksumAsset: source.checksumAsset ?? '',
		url: source.url ?? '',
		checksumUrl: source.checksumUrl ?? '',
		autoUpdate: source.autoUpdate
	};
}

export function formToRequest(form: SourceForm): SourceSaveRequest {
	const value = (text: string) => text.trim() || null;
	const request: SourceSaveRequest = {
		name: form.name.trim(),
		kind: form.kind,
		autoUpdate: form.autoUpdate
	};
	if (form.kind === 'GitHubRepository') {
		request.repository = value(form.repository);
		request.branch = value(form.branch);
	} else if (form.kind === 'GeositeDat' || form.kind === 'GeoipDat') {
		if (form.datMode === 'release') {
			request.releaseRepository = value(form.releaseRepository);
			request.asset = value(form.asset);
			request.checksumAsset = value(form.checksumAsset);
		} else {
			request.url = value(form.url);
			request.checksumUrl = value(form.checksumUrl);
		}
	}
	return request;
}
