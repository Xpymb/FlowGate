import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

const apiTarget = process.env.FLOWGATE_API ?? 'http://localhost:6000';
const proxy = { '/api': { target: apiTarget, changeOrigin: false } };

export default defineConfig({
	plugins: [
		sveltekit({
			compilerOptions: {
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},
			adapter: adapter({
				pages: 'dist',
				assets: 'dist',
				fallback: '200.html',
				precompress: true
			})
		})
	],
	server: { proxy },
	preview: { proxy }
});
