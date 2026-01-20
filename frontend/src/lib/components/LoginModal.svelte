<script lang="ts">
	import { isAdmin } from '$lib/stores/auth';

	let { onClose }: { onClose: () => void } = $props();

	let password = $state('');
	let error = $state('');
	let isLoading = $state(false);

	async function handleSubmit(e: Event) {
		e.preventDefault();
		error = '';
		isLoading = true;

		const success = await isAdmin.login(password);

		isLoading = false;

		if (success) {
			onClose();
		} else {
			error = 'Invalid password';
		}
	}

	function handleKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape') {
			onClose();
		}
	}

	function handleBackdropClick(e: MouseEvent) {
		if (e.target === e.currentTarget) {
			onClose();
		}
	}
</script>

<svelte:window onkeydown={handleKeydown} />

<div class="modal-backdrop" onclick={handleBackdropClick} role="presentation">
	<div class="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
		<h2 id="modal-title">Admin Login</h2>
		<form onsubmit={handleSubmit}>
			<div class="form-group">
				<label for="password">Password</label>
				<input
					type="password"
					id="password"
					bind:value={password}
					disabled={isLoading}
					autocomplete="current-password"
				/>
			</div>
			{#if error}
				<p class="error">{error}</p>
			{/if}
			<div class="actions">
				<button type="button" class="cancel-btn" onclick={onClose} disabled={isLoading}>
					Cancel
				</button>
				<button type="submit" class="login-btn" disabled={isLoading || !password}>
					{isLoading ? 'Logging in...' : 'Login'}
				</button>
			</div>
		</form>
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
		max-width: 360px;
		box-shadow: 0 4px 24px rgba(0, 0, 0, 0.2);
	}

	h2 {
		margin: 0 0 20px 0;
		font-size: 20px;
		font-weight: 600;
		color: var(--text-primary);
	}

	.form-group {
		margin-bottom: 16px;
	}

	label {
		display: block;
		margin-bottom: 6px;
		font-size: 14px;
		color: var(--text-secondary);
	}

	input {
		width: 100%;
		padding: 10px 12px;
		font-size: 14px;
		border: 1px solid var(--border-color);
		border-radius: 8px;
		background: var(--bg-primary);
		color: var(--text-primary);
		box-sizing: border-box;
	}

	input:focus {
		outline: none;
		border-color: var(--accent-color, #3b82f6);
	}

	input:disabled {
		opacity: 0.6;
	}

	.error {
		color: #ef4444;
		font-size: 14px;
		margin: 0 0 16px 0;
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

	.login-btn {
		background: var(--accent-color, #3b82f6);
		border: none;
		color: white;
	}

	.login-btn:hover:not(:disabled) {
		opacity: 0.9;
	}
</style>
