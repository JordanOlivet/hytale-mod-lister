<script lang="ts">
	import { onMount } from 'svelte';
	import Header from '$lib/components/Header.svelte';
	import ModsTable from '$lib/components/ModsTable.svelte';
	import { init } from '$lib/stores/mods';

	onMount(() => {
		init();
	});
</script>

<div class="app">
	<Header />
	<main>
		<ModsTable />
	</main>
</div>

<style>
	.app {
		min-height: 100vh;
		display: flex;
		flex-direction: column;
	}

	main {
		flex: 1;
	}
</style>
