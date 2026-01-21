<script lang="ts">
	import { filteredMods, searchQuery, sortColumn, sortDirection, isLoading, error, modsWithUpdates } from '$lib/stores/mods';
	import { isAdmin } from '$lib/stores/auth';
	import type { Mod } from '$lib/types';
	import UpdateModModal from './UpdateModModal.svelte';
	import BulkUpdateModal from './BulkUpdateModal.svelte';

	let showUpdateModal = $state(false);
	let selectedModForUpdate = $state<Mod | null>(null);
	let showBulkUpdateModal = $state(false);

	function openUpdateModal(mod: Mod) {
		selectedModForUpdate = mod;
		showUpdateModal = true;
	}

	function closeUpdateModal() {
		showUpdateModal = false;
		selectedModForUpdate = null;
	}

	function toggleSort(column: 'name' | 'version' | 'authors') {
		if ($sortColumn === column) {
			sortDirection.update(d => d === 'asc' ? 'desc' : 'asc');
		} else {
			sortColumn.set(column);
			sortDirection.set('asc');
		}
	}

	function getUrlDisplay(mod: Mod): { url: string; label: string; type: 'curseforge' | 'website' | 'none' } {
		if (mod.curseForgeUrl) {
			return { url: mod.curseForgeUrl, label: mod.curseForgeUrl, type: 'curseforge' };
		}
		if (mod.website) {
			return { url: mod.website, label: mod.website, type: 'website' };
		}
		return { url: '', label: '-', type: 'none' };
	}

	type BadgeType = 'manifest' | 'cache' | 'cf-name' | 'cf-id' | 'cf-partial' | 'cf-fuzzy' | 'website' | 'unknown';

	function getBadgeLabel(mod: Mod): { label: string; type: BadgeType } {
		if (!mod.curseForgeUrl && mod.website) return { label: 'website', type: 'website' };
		if (mod.foundVia === 'manifest') return { label: 'manifest', type: 'manifest' };
		if (mod.foundVia === 'cache') return { label: 'cache', type: 'cache' };
		if (mod.foundVia === 'exact') return { label: 'CurseForge: name', type: 'cf-name' };
		if (mod.foundVia === 'slug') return { label: 'CurseForge: id', type: 'cf-id' };
		if (mod.foundVia === 'substring') return { label: 'CurseForge: partial', type: 'cf-partial' };
		if (mod.foundVia?.startsWith('fuzzy')) {
			const percent = mod.foundVia.replace('fuzzy:', '');
			return { label: `CurseForge: ~${percent}`, type: 'cf-fuzzy' };
		}
		if (!mod.curseForgeUrl && !mod.website) return { label: 'unknown', type: 'unknown' };
		return { label: '', type: 'cf-name' };
	}

	function truncate(text: string | undefined, maxLength: number): string {
		if (!text) return '';
		return text.length > maxLength ? text.slice(0, maxLength) + '...' : text;
	}

	function getSortIcon(column: string): string {
		if ($sortColumn !== column) return '';
		return $sortDirection === 'asc' ? ' ↑' : ' ↓';
	}

	function hasUpdate(mod: Mod): boolean {
		if (!mod.latestCurseForgeVersion || !mod.version) return false;
		// Normalize versions for comparison (remove leading 'v' if present)
		const local = mod.version.replace(/^v/i, '').trim();
		const remote = mod.latestCurseForgeVersion.replace(/^v/i, '').trim();
		return local !== remote;
	}
</script>

<div class="table-container">
	<div class="search-bar">
		<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
			<circle cx="11" cy="11" r="8"></circle>
			<line x1="21" y1="21" x2="16.65" y2="16.65"></line>
		</svg>
		<input
			type="text"
			placeholder="Search mods..."
			bind:value={$searchQuery}
		/>
		{#if $isAdmin && $modsWithUpdates.length > 0}
			<button class="bulk-update-btn" onclick={() => showBulkUpdateModal = true}>
				Tout mettre à jour ({$modsWithUpdates.length})
			</button>
		{/if}
	</div>

	{#if $isLoading}
		<div class="loading">
			<div class="spinner"></div>
			<span>Loading mods...</span>
		</div>
	{:else if $error}
		<div class="error">
			<span>{$error}</span>
			<button onclick={() => window.location.reload()}>Retry</button>
		</div>
	{:else if $filteredMods.length === 0}
		<div class="empty">
			{#if $searchQuery}
				No mods found matching "{$searchQuery}"
			{:else}
				No mods found. Add .jar or .zip files to the mods folder.
			{/if}
		</div>
	{:else}
		<table>
			<thead>
				<tr>
					<th class="sortable" onclick={() => toggleSort('name')}>
						Name{getSortIcon('name')}
					</th>
					<th>URL</th>
					<th class="sortable" onclick={() => toggleSort('version')}>
						Version{getSortIcon('version')}
					</th>
					<th class="sortable" onclick={() => toggleSort('authors')}>
						Authors{getSortIcon('authors')}
					</th>
					<th>Description</th>
					<th>Source</th>
				</tr>
			</thead>
			<tbody>
				{#each $filteredMods as mod (mod.fileName)}
					{@const urlInfo = getUrlDisplay(mod)}
					{@const badge = getBadgeLabel(mod)}
					<tr>
						<td class="name-cell">{mod.name}</td>
						<td class="url-cell">
							{#if urlInfo.type !== 'none'}
								<a href={urlInfo.url} target="_blank" rel="noopener noreferrer" class="url-link" class:website={urlInfo.type === 'website'}>
									{truncate(urlInfo.label, 50)}
									<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
										<path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
										<polyline points="15 3 21 3 21 9"></polyline>
										<line x1="10" y1="14" x2="21" y2="3"></line>
									</svg>
								</a>
							{:else}
								<span class="no-url">-</span>
							{/if}
						</td>
						<td class="secondary">
							{mod.version}
							{#if hasUpdate(mod)}
								{#if $isAdmin}
									<button class="badge badge-update badge-clickable"
											title="Click to update to {mod.latestCurseForgeVersion}"
											onclick={() => openUpdateModal(mod)}>
										UPDATE
									</button>
								{:else}
									<span class="badge badge-update" title="New version: {mod.latestCurseForgeVersion}">UPDATE</span>
								{/if}
							{/if}
						</td>
						<td class="secondary">{mod.authors.join(', ') || 'Unknown'}</td>
						<td class="secondary description">{truncate(mod.description, 100)}</td>
						<td>
							{#if badge.label}
								<span class="badge badge-{badge.type}">{badge.label}</span>
							{/if}
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	{/if}
</div>

{#if showUpdateModal && selectedModForUpdate}
	<UpdateModModal mod={selectedModForUpdate} onClose={closeUpdateModal} />
{/if}

{#if showBulkUpdateModal}
	<BulkUpdateModal modsToUpdate={$modsWithUpdates} onClose={() => showBulkUpdateModal = false} />
{/if}

<style>
	.table-container {
		padding: 24px;
	}

	.search-bar {
		display: flex;
		align-items: center;
		gap: 12px;
		padding: 12px 16px;
		background: var(--bg-secondary);
		border: 1px solid var(--border-color);
		border-radius: 8px;
		margin-bottom: 24px;
	}

	.search-bar svg {
		color: var(--text-secondary);
	}

	.search-bar input {
		flex: 1;
		border: none;
		background: transparent;
		font-size: 14px;
		color: var(--text-primary);
		outline: none;
	}

	.search-bar input::placeholder {
		color: var(--text-secondary);
	}

	.bulk-update-btn {
		padding: 8px 16px;
		background: #16a34a;
		color: white;
		border: none;
		border-radius: 6px;
		font-size: 14px;
		font-weight: 500;
		cursor: pointer;
		margin-left: auto;
		white-space: nowrap;
	}

	.bulk-update-btn:hover {
		background: #15803d;
	}

	table {
		width: 100%;
		border-collapse: collapse;
	}

	th, td {
		padding: 12px 16px;
		text-align: left;
		border-bottom: 1px solid var(--border-color);
	}

	th {
		font-weight: 600;
		font-size: 13px;
		color: var(--text-secondary);
		text-transform: uppercase;
		letter-spacing: 0.5px;
		background: var(--bg-secondary);
	}

	th.sortable {
		cursor: pointer;
		user-select: none;
	}

	th.sortable:hover {
		color: var(--text-primary);
	}

	.name-cell {
		font-weight: 600;
		color: var(--text-primary);
	}

	.url-cell {
		max-width: 300px;
	}

	.url-link {
		display: inline-flex;
		align-items: center;
		gap: 6px;
		color: var(--accent-color);
		text-decoration: none;
		font-weight: 500;
	}

	.url-link:hover {
		text-decoration: underline;
	}

	.url-link.website {
		color: var(--text-secondary);
		font-weight: 400;
	}

	.no-url {
		color: var(--text-secondary);
	}

	.secondary {
		color: var(--text-secondary);
		font-size: 13px;
	}

	.description {
		max-width: 200px;
	}

	.badge {
		display: inline-block;
		padding: 2px 8px;
		font-size: 11px;
		font-weight: 500;
		border-radius: 4px;
		color: white;
		text-transform: uppercase;
	}

	/* Manifest: green - reliable source from the mod itself */
	.badge-manifest {
		background: #16a34a;
	}

	/* Cache: purple - cached data */
	.badge-cache {
		background: #7c3aed;
	}

	/* CurseForge sources: blue gradient from dark (most reliable) to light (least reliable) */
	.badge-cf-name {
		background: #1e40af;
	}

	.badge-cf-id {
		background: #2563eb;
	}

	.badge-cf-partial {
		background: #3b82f6;
	}

	.badge-cf-fuzzy {
		background: #60a5fa;
	}

	/* Website: gray - fallback */
	.badge-website {
		background: #6b7280;
	}

	/* Unknown: red - not found */
	.badge-unknown {
		background: #dc2626;
	}

	/* Update available: green */
	.badge-update {
		background: #16a34a;
		cursor: help;
		margin-left: 8px;
		vertical-align: middle;
	}

	/* Clickable badge for admin users */
	button.badge-clickable {
		cursor: pointer;
		border: none;
		transition: all 0.2s ease;
	}

	button.badge-clickable:hover {
		background: #15803d;
		transform: scale(1.05);
	}

	button.badge-clickable:active {
		transform: scale(0.98);
	}

	.loading, .error, .empty {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 16px;
		padding: 64px;
		color: var(--text-secondary);
	}

	.error {
		color: #ef4444;
	}

	.error button {
		padding: 8px 16px;
		background: #ef4444;
		color: white;
		border: none;
		border-radius: 6px;
		cursor: pointer;
	}

	.spinner {
		width: 32px;
		height: 32px;
		border: 3px solid var(--border-color);
		border-top-color: var(--accent-color);
		border-radius: 50%;
		animation: spin 1s linear infinite;
	}

	@keyframes spin {
		to { transform: rotate(360deg); }
	}
</style>
