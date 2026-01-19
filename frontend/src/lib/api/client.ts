import type { ModListResponse, StatusResponse } from '$lib/types';

const API_BASE = '/api';

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<T> {
	const response = await fetch(`${API_BASE}${endpoint}`, {
		headers: {
			'Content-Type': 'application/json',
			...options?.headers
		},
		...options
	});

	if (!response.ok) {
		throw new Error(`API error: ${response.status} ${response.statusText}`);
	}

	return response.json();
}

export async function getMods(): Promise<ModListResponse> {
	return fetchApi<ModListResponse>('/mods');
}

export async function getStatus(): Promise<StatusResponse> {
	return fetchApi<StatusResponse>('/status');
}

export async function refreshMods(force: boolean = false): Promise<void> {
	await fetchApi(`/mods/refresh${force ? '?force=true' : ''}`, {
		method: 'POST'
	});
}
