<script lang="ts">
	import '../app.css';
	import { theme } from '$lib/stores/theme';
	import { onMount } from 'svelte';

	let { children } = $props();

	onMount(() => {
		theme.init();
	});
</script>

<svelte:head>
	<title>Hytale Mod Lister</title>
	<meta name="description" content="View and manage your Hytale mods" />
</svelte:head>

{@render children()}
