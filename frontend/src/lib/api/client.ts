import type { ModListResponse, StatusResponse, LoginResponse, VerifyResponse, UpdateModResponse } from '$lib/types';

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

export async function login(password: string): Promise<LoginResponse> {
	return fetchApi<LoginResponse>('/auth/login', {
		method: 'POST',
		body: JSON.stringify({ password })
	});
}

export async function verifyToken(token: string): Promise<VerifyResponse> {
	return fetchApi<VerifyResponse>('/auth/verify', {
		method: 'POST',
		headers: {
			Authorization: `Bearer ${token}`
		}
	});
}

export async function logout(token: string): Promise<void> {
	await fetchApi('/auth/logout', {
		method: 'POST',
		headers: {
			Authorization: `Bearer ${token}`
		}
	});
}

export async function updateMod(fileName: string, token: string, skipRefresh: boolean = false): Promise<UpdateModResponse> {
	const query = skipRefresh ? '?skipRefresh=true' : '';
	return fetchApi<UpdateModResponse>(`/mods/${encodeURIComponent(fileName)}/update${query}`, {
		method: 'POST',
		headers: {
			Authorization: `Bearer ${token}`
		}
	});
}
