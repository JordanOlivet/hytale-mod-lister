<script lang="ts">
	import ThemeToggle from './ThemeToggle.svelte';
	import RefreshButton from './RefreshButton.svelte';
	import LoginButton from './LoginButton.svelte';
	import { lastUpdated } from '$lib/stores/mods';

	function formatDate(dateStr: string | null): string {
		if (!dateStr) return 'Never';
		const date = new Date(dateStr);
		return date.toLocaleString();
	}
</script>

<header class="header">
	<div class="header-left">
		<h1>Hytale Mod Lister <span class="version">v{__APP_VERSION__}</span></h1>
		<span class="last-updated">
			Last updated: {formatDate($lastUpdated)}
		</span>
	</div>
	<div class="header-right">
		<RefreshButton />
		<ThemeToggle />
		<LoginButton />
	</div>
</header>

<style>
	.header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		padding: 16px 24px;
		background: var(--bg-secondary);
		border-bottom: 1px solid var(--border-color);
		flex-shrink: 0;
	}

	.header-left {
		display: flex;
		flex-direction: column;
		gap: 4px;
	}

	h1 {
		margin: 0;
		font-size: 24px;
		font-weight: 600;
		color: var(--text-primary);
	}

	.version {
		font-size: 12px;
		font-weight: 400;
		color: var(--text-secondary);
		margin-left: 8px;
	}

	.last-updated {
		font-size: 13px;
		color: var(--text-secondary);
	}

	.header-right {
		display: flex;
		align-items: center;
		gap: 12px;
	}
</style>
