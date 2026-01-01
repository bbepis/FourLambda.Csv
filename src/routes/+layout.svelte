<script lang="ts">
	import './layout.css';
	import './microui.css';
	import favicon from '$lib/assets/icon.png';

	let { children } = $props();
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>
{@render children()}
