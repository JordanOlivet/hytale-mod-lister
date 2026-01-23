<script lang="ts">
	import { filteredMods, searchQuery, sortColumn, sortDirection, isLoading, error, modsWithUpdates } from '$lib/stores/mods';
	import { isAdmin } from '$lib/stores/auth';
	import type { Mod } from '$lib/types';
	import UpdateModModal from './UpdateModModal.svelte';
	import BulkUpdateModal from './BulkUpdateModal.svelte';
	import UrlOverrideModal from './UrlOverrideModal.svelte';
	import { hasNewerVersion } from '$lib/utils/versionCompare';

	let showUpdateModal = $state(false);
	let selectedModForUpdate = $state<Mod | null>(null);
	let showBulkUpdateModal = $state(false);
	let showOverrideModal = $state(false);
	let selectedModForOverride = $state<Mod | null>(null);

	function openUpdateModal(mod: Mod) {
		selectedModForUpdate = mod;
		showUpdateModal = true;
	}

	function closeUpdateModal() {
		showUpdateModal = false;
		selectedModForUpdate = null;
	}

	function openOverrideModal(mod: Mod) {
		selectedModForOverride = mod;
		showOverrideModal = true;
	}

	function closeOverrideModal() {
		showOverrideModal = false;
		selectedModForOverride = null;
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

	type BadgeType = 'manifest' | 'cache' | 'cf-name' | 'cf-id' | 'cf-partial' | 'cf-fuzzy' | 'website' | 'unknown' | 'override';

	function getBadgeLabel(mod: Mod): { label: string; type: BadgeType } {
		if (mod.foundVia === 'override') return { label: 'override', type: 'override' };
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
		return hasNewerVersion(mod.version, mod.latestCurseForgeVersion);
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
		<div class="table-wrapper">
			<table>
				<thead>
				<tr>
					<th class="sortable col-name" onclick={() => toggleSort('name')}>
						Name{getSortIcon('name')}
					</th>
					<th class="col-url">URL</th>
					<th class="sortable col-version" onclick={() => toggleSort('version')}>
						Version{getSortIcon('version')}
					</th>
					<th class="sortable col-authors" onclick={() => toggleSort('authors')}>
						Authors{getSortIcon('authors')}
					</th>
					<th class="col-description">Description</th>
					<th class="col-source">Source</th>
				</tr>
			</thead>
			<tbody>
				{#each $filteredMods as mod (mod.fileName)}
					{@const urlInfo = getUrlDisplay(mod)}
					{@const badge = getBadgeLabel(mod)}
					<tr>
						<td class="name-cell col-name">{mod.name}</td>
						<td class="url-cell col-url">
							{#if urlInfo.type !== 'none'}
								<a href={urlInfo.url} target="_blank" rel="noopener noreferrer" class="url-link" class:website={urlInfo.type === 'website'} title={urlInfo.url}>
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
						<td class="version-cell col-version" title={mod.version}>
							<span class="version-text">{mod.version}</span>
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
						<td class="secondary col-authors">{mod.authors.join(', ') || 'Unknown'}</td>
						<td class="secondary description col-description">{truncate(mod.description, 100)}</td>
						<td class="col-source">
							<span class="source-cell">
								{#if badge.label}
									<span class="badge badge-{badge.type}">{badge.label}</span>
								{/if}
								{#if $isAdmin}
									<button
										class="edit-override-btn"
										class:active={mod.foundVia === 'override'}
										title="Edit URL override"
										onclick={() => openOverrideModal(mod)}
									>
										<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
											<path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/>
											<path d="m15 5 4 4"/>
										</svg>
									</button>
								{/if}
							</span>
						</td>
					</tr>
				{/each}
				</tbody>
			</table>
		</div>
	{/if}
</div>

{#if showUpdateModal && selectedModForUpdate}
	<UpdateModModal mod={selectedModForUpdate} onClose={closeUpdateModal} />
{/if}

{#if showBulkUpdateModal}
	<BulkUpdateModal modsToUpdate={$modsWithUpdates} onClose={() => showBulkUpdateModal = false} />
{/if}

{#if showOverrideModal && selectedModForOverride}
	<UrlOverrideModal mod={selectedModForOverride} onClose={closeOverrideModal} />
{/if}

<style>
	.table-container {
		padding: 24px;
		flex: 1;
		display: flex;
		flex-direction: column;
		overflow: hidden;
		min-height: 0;
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
		flex-shrink: 0;
	}

	.table-wrapper {
		flex: 1;
		overflow: hidden;
		min-height: 0;
		display: flex;
		flex-direction: column;
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
		border-collapse: separate;
		border-spacing: 0;
		display: flex;
		flex-direction: column;
		flex: 1;
		min-height: 0;
	}

	thead {
		flex-shrink: 0;
	}


	tbody {
		flex: 1;
		overflow-y: auto;
		min-height: 0;
	}

	thead tr,
	tbody tr {
		display: table;
		width: 100%;
		table-layout: fixed;
	}

	.col-name { width: 14%; word-wrap: break-word; overflow-wrap: break-word; }
	.col-url { width: 25%; word-wrap: break-word; overflow-wrap: break-word; }
	.col-version { width: 12%; }
	.col-authors { width: 12%; }
	.col-description { width: 22%; }
	.col-source { width: 15%; }

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
		word-wrap: break-word;
		overflow-wrap: break-word;
	}

	.url-link {
		display: inline;
		word-break: break-all;
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

	.version-cell {
		color: var(--text-secondary);
		font-size: 13px;
		min-width: 120px;
		max-width: 180px;
	}

	.version-text {
		display: inline-block;
		max-width: 100px;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
		vertical-align: middle;
	}

	/* Show full version on hover */
	.version-cell:hover .version-text {
		max-width: none;
		overflow: visible;
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

	/* Override: orange - manually set */
	.badge-override {
		background: #f59e0b;
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

	.source-cell {
		display: inline-flex;
		align-items: center;
		gap: 8px;
	}

	.edit-override-btn {
		display: flex;
		align-items: center;
		justify-content: center;
		width: 24px;
		height: 24px;
		padding: 0;
		border: none;
		border-radius: 4px;
		background: transparent;
		color: var(--text-secondary);
		cursor: pointer;
		transition: all 0.2s ease;
		opacity: 0.5;
	}

	.edit-override-btn:hover {
		background: var(--bg-secondary);
		color: var(--text-primary);
		opacity: 1;
	}

	.edit-override-btn.active {
		color: #f59e0b;
		opacity: 1;
	}

	.edit-override-btn.active:hover {
		color: #d97706;
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
