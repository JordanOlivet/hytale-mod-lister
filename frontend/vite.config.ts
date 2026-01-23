import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';
import { readFileSync } from 'fs';

const pkg = JSON.parse(readFileSync('./package.json', 'utf-8'));

export default defineConfig({
	plugins: [sveltekit()],
	define: {
		__APP_VERSION__: JSON.stringify(pkg.version)
	},
	server: {
		proxy: {
			'/api': {
				target: 'http://localhost:5000',
				changeOrigin: true
			},
			'/health': {
				target: 'http://localhost:5000',
				changeOrigin: true
			}
		}
	}
});
