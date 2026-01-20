<script lang="ts">
	import { isAdmin } from '$lib/stores/auth';
	import LoginModal from './LoginModal.svelte';

	let showModal = $state(false);

	function handleClick() {
		if ($isAdmin) {
			isAdmin.logout();
		} else {
			showModal = true;
		}
	}
</script>

<button
	class="login-button"
	onclick={handleClick}
	aria-label={$isAdmin ? 'Logout' : 'Login'}
	title={$isAdmin ? 'Logout' : 'Admin Login'}
>
	{#if $isAdmin}
		<!-- Unlocked padlock icon -->
		<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
			<rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
			<path d="M7 11V7a5 5 0 0 1 9.9-1"></path>
		</svg>
	{:else}
		<!-- Locked padlock icon -->
		<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
			<rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
			<path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
		</svg>
	{/if}
</button>

{#if showModal}
	<LoginModal onClose={() => (showModal = false)} />
{/if}

<style>
	.login-button {
		background: none;
		border: 1px solid var(--border-color);
		border-radius: 8px;
		padding: 8px;
		cursor: pointer;
		color: var(--text-secondary);
		display: flex;
		align-items: center;
		justify-content: center;
		transition: all 0.2s ease;
	}

	.login-button:hover {
		color: var(--text-primary);
		border-color: var(--text-secondary);
	}
</style>
