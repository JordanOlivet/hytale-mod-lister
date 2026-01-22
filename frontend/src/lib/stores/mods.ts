import { writable, derived, get } from 'svelte/store';
import type { Mod, StatusResponse } from '$lib/types';
import { getMods, getStatus, refreshMods as apiRefresh } from '$lib/api/client';
import { hasNewerVersion } from '$lib/utils/versionCompare';

export const mods = writable<Mod[]>([]);
export const lastUpdated = writable<string | null>(null);
export const isLoading = writable(true);
export const error = writable<string | null>(null);
export const status = writable<StatusResponse | null>(null);

export const searchQuery = writable('');
export const sortColumn = writable<'name' | 'version' | 'authors'>('name');
export const sortDirection = writable<'asc' | 'desc'>('asc');

// Polling state
let pollInterval: ReturnType<typeof setInterval> | null = null;
let isPolling = false;

export const modsWithUpdates = derived(mods, ($mods) => {
	return $mods.filter((mod) => hasNewerVersion(mod.version, mod.latestCurseForgeVersion));
});

export const filteredMods = derived(
	[mods, searchQuery, sortColumn, sortDirection],
	([$mods, $searchQuery, $sortColumn, $sortDirection]) => {
		let result = [...$mods];

		// Filter by search query
		if ($searchQuery) {
			const query = $searchQuery.toLowerCase();
			result = result.filter(
				(mod) =>
					mod.name.toLowerCase().includes(query) ||
					mod.authors.some((a) => a.toLowerCase().includes(query)) ||
					mod.description?.toLowerCase().includes(query)
			);
		}

		// Sort
		result.sort((a, b) => {
			let comparison = 0;
			switch ($sortColumn) {
				case 'name':
					comparison = a.name.localeCompare(b.name);
					break;
				case 'version':
					comparison = a.version.localeCompare(b.version);
					break;
				case 'authors':
					comparison = (a.authors[0] ?? '').localeCompare(b.authors[0] ?? '');
					break;
			}
			return $sortDirection === 'asc' ? comparison : -comparison;
		});

		return result;
	}
);

export async function loadMods() {
	isLoading.set(true);
	error.set(null);

	try {
		const response = await getMods();
		mods.set(response.mods);
		lastUpdated.set(response.lastUpdated);
	} catch (e) {
		error.set(e instanceof Error ? e.message : 'Failed to load mods');
	} finally {
		isLoading.set(false);
	}
}

export async function loadStatus() {
	try {
		const response = await getStatus();
		status.set(response);

		// Auto-start polling if refresh is in progress
		if (response.isRefreshing && !isPolling) {
			startPolling();
		}

		return response;
	} catch (e) {
		console.error('Failed to load status:', e);
		return null;
	}
}

export async function refreshMods(force: boolean = false) {
	try {
		await apiRefresh(force);
		// Start polling after triggering refresh
		startPolling();
	} catch (e) {
		error.set(e instanceof Error ? e.message : 'Failed to trigger refresh');
	}
}

function startPolling() {
	if (isPolling) return;
	isPolling = true;
	console.log('[Polling] Started');

	pollInterval = setInterval(async () => {
		const currentStatus = await loadStatus();
		console.log('[Polling] Status:', currentStatus?.isRefreshing, currentStatus?.progress);

		if (currentStatus && !currentStatus.isRefreshing) {
			stopPolling();
			// Reload mods when refresh is complete
			await loadMods();
			console.log('[Polling] Refresh complete, mods reloaded');
		}
	}, 1000);
}

function stopPolling() {
	if (!isPolling) return;
	isPolling = false;

	if (pollInterval) {
		clearInterval(pollInterval);
		pollInterval = null;
	}
	console.log('[Polling] Stopped');
}

// Initialize: load status and start polling if needed
export async function init() {
	await Promise.all([loadMods(), loadStatus()]);
}
