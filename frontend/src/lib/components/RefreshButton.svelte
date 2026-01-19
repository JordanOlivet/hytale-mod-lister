<script lang="ts">
	import { status, refreshMods } from '$lib/stores/mods';
	import type { StatusResponse } from '$lib/types';

	let dropdownOpen = $state(false);
	let currentStatus = $state<StatusResponse | null>(null);

	$effect(() => {
		const unsubscribe = status.subscribe((value) => {
			currentStatus = value;
		});
		return unsubscribe;
	});

	let isRefreshing = $derived(currentStatus?.isRefreshing ?? false);
	let progress = $derived(currentStatus?.progress);

	async function handleRefresh() {
		await refreshMods(false);
	}

	async function handleForceRefresh() {
		dropdownOpen = false;
		await refreshMods(true);
	}

	function toggleDropdown(e: MouseEvent) {
		e.stopPropagation();
		dropdownOpen = !dropdownOpen;
	}

	function closeDropdown() {
		dropdownOpen = false;
	}
</script>

<svelte:window onclick={closeDropdown} />

<div class="refresh-container">
	<button
		class="refresh-button"
		onclick={handleRefresh}
		disabled={isRefreshing}
		aria-label="Refresh mods"
	>
		{#if isRefreshing}
			<svg class="spinner" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
				<path d="M21 12a9 9 0 1 1-6.219-8.56"/>
			</svg>
			<span>
				{#if progress}
					{progress.processed}/{progress.total}
				{:else}
					Refreshing...
				{/if}
			</span>
		{:else}
			<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
				<polyline points="23 4 23 10 17 10"></polyline>
				<polyline points="1 20 1 14 7 14"></polyline>
				<path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>
			</svg>
			<span>Refresh</span>
		{/if}
	</button>

	<button
		class="dropdown-toggle"
		onclick={toggleDropdown}
		disabled={isRefreshing}
		aria-label="More refresh options"
		aria-expanded={dropdownOpen}
	>
		<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
			<polyline points="6 9 12 15 18 9"></polyline>
		</svg>
	</button>

	{#if dropdownOpen}
		<div class="dropdown-menu">
			<button class="dropdown-item" onclick={handleForceRefresh}>
				<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
					<path d="M21 2v6h-6"></path>
					<path d="M3 12a9 9 0 0 1 15-6.7L21 8"></path>
					<path d="M3 22v-6h6"></path>
					<path d="M21 12a9 9 0 0 1-15 6.7L3 16"></path>
				</svg>
				Force Refresh
				<span class="dropdown-hint">Ignore cache</span>
			</button>
		</div>
	{/if}
</div>

<style>
	.refresh-container {
		position: relative;
		display: flex;
	}

	.refresh-button {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 8px 16px;
		background: var(--accent-color);
		color: white;
		border: none;
		border-radius: 8px 0 0 8px;
		font-size: 14px;
		font-weight: 500;
		cursor: pointer;
		transition: all 0.2s ease;
	}

	.refresh-button:hover:not(:disabled) {
		opacity: 0.9;
	}

	.refresh-button:disabled {
		opacity: 0.7;
		cursor: not-allowed;
	}

	.dropdown-toggle {
		display: flex;
		align-items: center;
		justify-content: center;
		padding: 8px 10px;
		background: var(--accent-color);
		color: white;
		border: none;
		border-left: 1px solid rgba(255, 255, 255, 0.2);
		border-radius: 0 8px 8px 0;
		cursor: pointer;
		transition: all 0.2s ease;
	}

	.dropdown-toggle:hover:not(:disabled) {
		background: color-mix(in srgb, var(--accent-color) 85%, black);
	}

	.dropdown-toggle:disabled {
		opacity: 0.7;
		cursor: not-allowed;
	}

	.dropdown-menu {
		position: absolute;
		top: 100%;
		right: 0;
		margin-top: 4px;
		background: var(--bg-primary);
		border: 1px solid var(--border-color);
		border-radius: 6px;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
		z-index: 100;
		overflow: hidden;
	}

	.dropdown-item {
		display: flex;
		align-items: center;
		gap: 8px;
		width: 100%;
		padding: 8px 12px;
		background: none;
		border: none;
		color: var(--text-primary);
		font-size: 13px;
		cursor: pointer;
		transition: background 0.15s ease;
		white-space: nowrap;
	}

	.dropdown-item:hover {
		background: var(--bg-secondary);
	}

	.dropdown-hint {
		margin-left: 8px;
		font-size: 11px;
		color: var(--text-secondary);
		opacity: 0.8;
	}

	.spinner {
		animation: spin 1s linear infinite;
	}

	@keyframes spin {
		from { transform: rotate(0deg); }
		to { transform: rotate(360deg); }
	}
</style>
