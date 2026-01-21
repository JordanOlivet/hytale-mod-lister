<script lang="ts">
	import { isAdmin } from '$lib/stores/auth';
	import { updateMod } from '$lib/api/client';
	import { refreshMods } from '$lib/stores/mods';
	import type { Mod } from '$lib/types';

	interface ModStatus {
		mod: Mod;
		selected: boolean;
		status: 'pending' | 'in_progress' | 'success' | 'error';
		error?: string;
	}

	let { modsToUpdate, onClose }: { modsToUpdate: Mod[]; onClose: () => void } = $props();

	let modStatuses = $state<ModStatus[]>(
		modsToUpdate.map((mod) => ({
			mod,
			selected: true,
			status: 'pending' as const
		}))
	);

	let isUpdating = $state(false);
	let isComplete = $state(false);
	let currentIndex = $state(0);

	const selectedCount = $derived(modStatuses.filter((m) => m.selected).length);
	const successCount = $derived(modStatuses.filter((m) => m.status === 'success').length);
	const errorCount = $derived(modStatuses.filter((m) => m.status === 'error').length);

	function toggleAll() {
		const allSelected = modStatuses.every((m) => m.selected);
		modStatuses = modStatuses.map((m) => ({ ...m, selected: !allSelected }));
	}

	function toggleMod(index: number) {
		modStatuses[index].selected = !modStatuses[index].selected;
	}

	async function handleUpdate() {
		const token = isAdmin.getToken();
		if (!token) {
			modStatuses = modStatuses.map((m) => ({
				...m,
				status: 'error' as const,
				error: 'Session expirée. Veuillez vous reconnecter.'
			}));
			isComplete = true;
			return;
		}

		isUpdating = true;
		currentIndex = 0;

		const selectedMods = modStatuses
			.map((m, i) => ({ ...m, originalIndex: i }))
			.filter((m) => m.selected);

		for (let i = 0; i < selectedMods.length; i++) {
			const { mod, originalIndex } = selectedMods[i];
			currentIndex = i + 1;

			modStatuses[originalIndex].status = 'in_progress';

			try {
				// Skip refresh for all updates (we'll refresh once at the end)
				const result = await updateMod(mod.fileName, token, true);

				if (result.success) {
					modStatuses[originalIndex].status = 'success';
				} else {
					modStatuses[originalIndex].status = 'error';
					modStatuses[originalIndex].error = result.message || 'Échec de la mise à jour';
				}
			} catch (e) {
				modStatuses[originalIndex].status = 'error';
				modStatuses[originalIndex].error =
					e instanceof Error ? e.message : 'Erreur inattendue';

				// Stop on authentication error
				if (e instanceof Error && e.message.includes('401')) {
					break;
				}
			}
		}

		isComplete = true;
		isUpdating = false;

		// Trigger a backend refresh to update the mods list with new versions
		await refreshMods();
	}

	function handleKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape' && !isUpdating) {
			onClose();
		}
	}

	function handleBackdropClick(e: MouseEvent) {
		if (e.target === e.currentTarget && !isUpdating) {
			onClose();
		}
	}
</script>

<svelte:window onkeydown={handleKeydown} />

<div class="modal-backdrop" onclick={handleBackdropClick} role="presentation">
	<div class="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
		{#if isComplete}
			<h2 id="modal-title">Mise à jour terminée</h2>
			<p class="summary">
				{successCount} réussi{successCount > 1 ? 's' : ''}{#if errorCount > 0}, {errorCount} échoué{errorCount > 1 ? 's' : ''}{/if}
			</p>
			<div class="mod-list">
				{#each modStatuses.filter((m) => m.selected || m.status !== 'pending') as modStatus}
					<div class="mod-row result">
						<span class="status-icon" class:success={modStatus.status === 'success'} class:error={modStatus.status === 'error'}>
							{#if modStatus.status === 'success'}
								<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
									<polyline points="20 6 9 17 4 12"></polyline>
								</svg>
							{:else if modStatus.status === 'error'}
								<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
									<line x1="18" y1="6" x2="6" y2="18"></line>
									<line x1="6" y1="6" x2="18" y2="18"></line>
								</svg>
							{/if}
						</span>
						<span class="mod-name">{modStatus.mod.name}</span>
						{#if modStatus.error}
							<span class="error-text">- {modStatus.error}</span>
						{/if}
					</div>
				{/each}
			</div>
			<div class="actions">
				<button type="button" class="confirm-btn" onclick={onClose}>
					Fermer
				</button>
			</div>
		{:else if isUpdating}
			<h2 id="modal-title">Mise à jour en cours...</h2>
			<p class="progress">Mise à jour {currentIndex}/{selectedCount}</p>
			<div class="mod-list">
				{#each modStatuses.filter((m) => m.selected) as modStatus}
					<div class="mod-row">
						<span class="status-icon" class:success={modStatus.status === 'success'} class:error={modStatus.status === 'error'} class:in-progress={modStatus.status === 'in_progress'}>
							{#if modStatus.status === 'success'}
								<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
									<polyline points="20 6 9 17 4 12"></polyline>
								</svg>
							{:else if modStatus.status === 'error'}
								<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
									<line x1="18" y1="6" x2="6" y2="18"></line>
									<line x1="6" y1="6" x2="18" y2="18"></line>
								</svg>
							{:else if modStatus.status === 'in_progress'}
								<span class="spinner"></span>
							{:else}
								<span class="pending-dot"></span>
							{/if}
						</span>
						<span class="mod-name">{modStatus.mod.name}</span>
						<span class="status-text">
							{#if modStatus.status === 'success'}
								- Mis à jour
							{:else if modStatus.status === 'error'}
								- Échec
							{:else if modStatus.status === 'in_progress'}
								- En cours...
							{:else}
								- En attente
							{/if}
						</span>
					</div>
				{/each}
			</div>
		{:else}
			<h2 id="modal-title">Mettre à jour les mods ({modsToUpdate.length})</h2>

			<label class="select-all">
				<input
					type="checkbox"
					checked={modStatuses.every((m) => m.selected)}
					indeterminate={modStatuses.some((m) => m.selected) && !modStatuses.every((m) => m.selected)}
					onchange={toggleAll}
				/>
				Tout sélectionner
			</label>

			<div class="mod-list">
				{#each modStatuses as modStatus, index}
					<label class="mod-row selectable">
						<input
							type="checkbox"
							checked={modStatus.selected}
							onchange={() => toggleMod(index)}
						/>
						<span class="mod-name">{modStatus.mod.name}</span>
						<span class="version-info">
							<span class="version current">{modStatus.mod.version}</span>
							<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
								<line x1="5" y1="12" x2="19" y2="12"></line>
								<polyline points="12 5 19 12 12 19"></polyline>
							</svg>
							<span class="version new">{modStatus.mod.latestCurseForgeVersion}</span>
						</span>
					</label>
				{/each}
			</div>

			<div class="actions">
				<button type="button" class="cancel-btn" onclick={onClose}>
					Annuler
				</button>
				<button
					type="button"
					class="confirm-btn"
					onclick={handleUpdate}
					disabled={selectedCount === 0}
				>
					Mettre à jour ({selectedCount})
				</button>
			</div>
		{/if}
	</div>
</div>

<style>
	.modal-backdrop {
		position: fixed;
		inset: 0;
		background: rgba(0, 0, 0, 0.5);
		display: flex;
		align-items: center;
		justify-content: center;
		z-index: 1000;
	}

	.modal {
		background: var(--bg-secondary);
		border: 1px solid var(--border-color);
		border-radius: 12px;
		padding: 24px;
		width: 100%;
		max-width: 500px;
		max-height: 80vh;
		display: flex;
		flex-direction: column;
		box-shadow: 0 4px 24px rgba(0, 0, 0, 0.2);
	}

	h2 {
		margin: 0 0 16px 0;
		font-size: 20px;
		font-weight: 600;
		color: var(--text-primary);
	}

	.summary {
		color: var(--text-secondary);
		margin: 0 0 16px 0;
		font-size: 14px;
	}

	.progress {
		color: var(--text-secondary);
		margin: 0 0 16px 0;
		font-size: 14px;
	}

	.select-all {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 8px 0;
		margin-bottom: 8px;
		font-size: 14px;
		color: var(--text-primary);
		cursor: pointer;
		border-bottom: 1px solid var(--border-color);
	}

	.mod-list {
		flex: 1;
		overflow-y: auto;
		overflow-x: hidden;
		margin-bottom: 20px;
	}

	.mod-row {
		display: flex;
		align-items: center;
		gap: 12px;
		padding: 10px 4px;
		border-bottom: 1px solid var(--border-color);
		border-radius: 4px;
	}

	.mod-row:last-child {
		border-bottom: none;
	}

	.mod-row.selectable {
		cursor: pointer;
	}

	.mod-row.selectable:hover {
		background: var(--bg-primary);
	}

	.mod-name {
		flex: 1;
		font-weight: 500;
		color: var(--text-primary);
		font-size: 14px;
	}

	.version-info {
		display: flex;
		align-items: center;
		gap: 8px;
	}

	.version-info svg {
		color: var(--text-secondary);
	}

	.version {
		font-family: monospace;
		font-size: 12px;
		padding: 2px 6px;
		border-radius: 4px;
	}

	.version.current {
		background: #374151;
		color: var(--text-secondary);
	}

	.version.new {
		background: #16a34a;
		color: white;
	}

	.status-icon {
		width: 20px;
		height: 20px;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.status-icon.success {
		color: #16a34a;
	}

	.status-icon.error {
		color: #ef4444;
	}

	.status-icon.in-progress {
		color: var(--accent-color);
	}

	.status-text {
		color: var(--text-secondary);
		font-size: 13px;
	}

	.error-text {
		color: #ef4444;
		font-size: 12px;
	}

	.pending-dot {
		width: 8px;
		height: 8px;
		border-radius: 50%;
		background: var(--text-secondary);
		opacity: 0.5;
	}

	.spinner {
		width: 14px;
		height: 14px;
		border: 2px solid var(--border-color);
		border-top-color: var(--accent-color);
		border-radius: 50%;
		animation: spin 1s linear infinite;
	}

	.actions {
		display: flex;
		gap: 12px;
		justify-content: flex-end;
	}

	button {
		padding: 10px 20px;
		font-size: 14px;
		font-weight: 500;
		border-radius: 8px;
		cursor: pointer;
		transition: all 0.2s ease;
	}

	button:disabled {
		opacity: 0.6;
		cursor: not-allowed;
	}

	.cancel-btn {
		background: none;
		border: 1px solid var(--border-color);
		color: var(--text-secondary);
	}

	.cancel-btn:hover:not(:disabled) {
		border-color: var(--text-secondary);
		color: var(--text-primary);
	}

	.confirm-btn {
		background: #16a34a;
		border: none;
		color: white;
	}

	.confirm-btn:hover:not(:disabled) {
		background: #15803d;
	}

	input[type='checkbox'] {
		width: 16px;
		height: 16px;
		cursor: pointer;
	}

	@keyframes spin {
		to {
			transform: rotate(360deg);
		}
	}
</style>
