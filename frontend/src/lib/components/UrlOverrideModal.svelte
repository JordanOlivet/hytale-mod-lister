<script lang="ts">
	import { isAdmin } from '$lib/stores/auth';
	import { getUrlOverride, setUrlOverride, deleteUrlOverride } from '$lib/api/client';
	import { loadMods } from '$lib/stores/mods';
	import type { Mod } from '$lib/types';

	let { mod, onClose }: { mod: Mod; onClose: () => void } = $props();

	let isLoading = $state(true);
	let isSaving = $state(false);
	let isDeleting = $state(false);
	let error = $state('');
	let success = $state(false);
	let successMessage = $state('');

	let urlInput = $state('');
	let existingOverride = $state(false);

	const urlPattern = /curseforge\.com\/hytale\/mods\//;

	let isValidUrl = $derived(urlPattern.test(urlInput));

	$effect(() => {
		loadExistingOverride();
	});

	async function loadExistingOverride() {
		isLoading = true;
		error = '';

		try {
			const override = await getUrlOverride(mod.name);
			if (override) {
				urlInput = override.curseForgeUrl;
				existingOverride = true;
			} else {
				urlInput = mod.curseForgeUrl || '';
				existingOverride = false;
			}
		} catch (e) {
			if (e instanceof Error) {
				error = e.message;
			}
		} finally {
			isLoading = false;
		}
	}

	async function handleSave() {
		error = '';
		isSaving = true;

		const token = isAdmin.getToken();
		if (!token) {
			error = 'Session expired. Please log in again.';
			isSaving = false;
			return;
		}

		try {
			await setUrlOverride(mod.name, urlInput, token);
			success = true;
			successMessage = 'Override saved!';
			await loadMods();
			setTimeout(() => {
				onClose();
			}, 1500);
		} catch (e) {
			if (e instanceof Error) {
				error = e.message;
			} else {
				error = 'An unexpected error occurred';
			}
		} finally {
			isSaving = false;
		}
	}

	async function handleDelete() {
		error = '';
		isDeleting = true;

		const token = isAdmin.getToken();
		if (!token) {
			error = 'Session expired. Please log in again.';
			isDeleting = false;
			return;
		}

		try {
			await deleteUrlOverride(mod.name, token);
			success = true;
			successMessage = 'Override deleted!';
			await loadMods();
			setTimeout(() => {
				onClose();
			}, 1500);
		} catch (e) {
			if (e instanceof Error) {
				error = e.message;
			} else {
				error = 'An unexpected error occurred';
			}
		} finally {
			isDeleting = false;
		}
	}

	function handleKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape' && !isSaving && !isDeleting) {
			onClose();
		}
	}

	function handleBackdropClick(e: MouseEvent) {
		if (e.target === e.currentTarget && !isSaving && !isDeleting) {
			onClose();
		}
	}

	let isWorking = $derived(isSaving || isDeleting);
</script>

<svelte:window onkeydown={handleKeydown} />

<div class="modal-backdrop" onclick={handleBackdropClick} role="presentation">
	<div class="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
		<h2 id="modal-title">URL Override</h2>

		{#if success}
			<div class="success-message">
				<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
					<path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
					<polyline points="22 4 12 14.01 9 11.01"></polyline>
				</svg>
				<span>{successMessage}</span>
			</div>
		{:else if isLoading}
			<div class="loading">
				<span class="spinner"></span>
				<span>Loading...</span>
			</div>
		{:else}
			<div class="mod-info">
				<p class="mod-name">{mod.name}</p>
				{#if existingOverride}
					<span class="badge badge-override">Override active</span>
				{/if}
			</div>

			<div class="input-group">
				<label for="url-input">CurseForge URL</label>
				<input
					id="url-input"
					type="text"
					bind:value={urlInput}
					placeholder="https://www.curseforge.com/hytale/mods/..."
					disabled={isWorking}
				/>
				{#if urlInput && !isValidUrl}
					<p class="hint error-hint">URL must contain "curseforge.com/hytale/mods/"</p>
				{/if}
			</div>

			{#if error}
				<p class="error">{error}</p>
			{/if}

			<div class="actions">
				{#if existingOverride}
					<button type="button" class="delete-btn" onclick={handleDelete} disabled={isWorking}>
						{#if isDeleting}
							<span class="spinner"></span>
							Deleting...
						{:else}
							Delete
						{/if}
					</button>
				{/if}
				<div class="spacer"></div>
				<button type="button" class="cancel-btn" onclick={onClose} disabled={isWorking}>
					Cancel
				</button>
				<button type="button" class="save-btn" onclick={handleSave} disabled={isWorking || !isValidUrl}>
					{#if isSaving}
						<span class="spinner"></span>
						Saving...
					{:else}
						Save
					{/if}
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
		box-shadow: 0 4px 24px rgba(0, 0, 0, 0.2);
	}

	h2 {
		margin: 0 0 20px 0;
		font-size: 20px;
		font-weight: 600;
		color: var(--text-primary);
	}

	.mod-info {
		margin-bottom: 20px;
		display: flex;
		align-items: center;
		gap: 12px;
	}

	.mod-name {
		font-size: 16px;
		font-weight: 600;
		color: var(--text-primary);
		margin: 0;
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

	.badge-override {
		background: #f59e0b;
	}

	.input-group {
		margin-bottom: 16px;
	}

	.input-group label {
		display: block;
		font-size: 14px;
		font-weight: 500;
		color: var(--text-secondary);
		margin-bottom: 8px;
	}

	.input-group input {
		width: 100%;
		padding: 10px 12px;
		font-size: 14px;
		border: 1px solid var(--border-color);
		border-radius: 8px;
		background: var(--bg-primary);
		color: var(--text-primary);
		outline: none;
		box-sizing: border-box;
	}

	.input-group input:focus {
		border-color: var(--accent-color);
	}

	.input-group input:disabled {
		opacity: 0.6;
	}

	.hint {
		font-size: 12px;
		margin: 6px 0 0 0;
		color: var(--text-secondary);
	}

	.error-hint {
		color: #ef4444;
	}

	.error {
		color: #ef4444;
		font-size: 14px;
		margin: 0 0 16px 0;
		padding: 10px;
		background: rgba(239, 68, 68, 0.1);
		border-radius: 6px;
	}

	.success-message {
		display: flex;
		align-items: center;
		justify-content: center;
		gap: 12px;
		padding: 24px;
		color: #16a34a;
		font-size: 16px;
		font-weight: 500;
	}

	.loading {
		display: flex;
		align-items: center;
		justify-content: center;
		gap: 12px;
		padding: 24px;
		color: var(--text-secondary);
	}

	.actions {
		display: flex;
		gap: 12px;
		align-items: center;
	}

	.spacer {
		flex: 1;
	}

	button {
		padding: 10px 20px;
		font-size: 14px;
		font-weight: 500;
		border-radius: 8px;
		cursor: pointer;
		transition: all 0.2s ease;
		display: flex;
		align-items: center;
		gap: 8px;
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

	.save-btn {
		background: #f59e0b;
		border: none;
		color: white;
	}

	.save-btn:hover:not(:disabled) {
		background: #d97706;
	}

	.delete-btn {
		background: #ef4444;
		border: none;
		color: white;
	}

	.delete-btn:hover:not(:disabled) {
		background: #dc2626;
	}

	.spinner {
		width: 14px;
		height: 14px;
		border: 2px solid rgba(255, 255, 255, 0.3);
		border-top-color: white;
		border-radius: 50%;
		animation: spin 1s linear infinite;
	}

	.loading .spinner {
		width: 20px;
		height: 20px;
		border-color: var(--border-color);
		border-top-color: var(--accent-color);
	}

	@keyframes spin {
		to { transform: rotate(360deg); }
	}
</style>
