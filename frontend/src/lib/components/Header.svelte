<script lang="ts">
	import ThemeToggle from './ThemeToggle.svelte';
	import RefreshButton from './RefreshButton.svelte';
	import { lastUpdated } from '$lib/stores/mods';

	function formatDate(dateStr: string | null): string {
		if (!dateStr) return 'Never';
		const date = new Date(dateStr);
		return date.toLocaleString();
	}
</script>

<header class="header">
	<div class="header-left">
		<h1>Hytale Mod Lister</h1>
		<span class="last-updated">
			Last updated: {formatDate($lastUpdated)}
		</span>
	</div>
	<div class="header-right">
		<RefreshButton />
		<ThemeToggle />
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
