import { writable } from 'svelte/store';
import { browser } from '$app/environment';
import { login as apiLogin, verifyToken, logout as apiLogout } from '$lib/api/client';

const TOKEN_KEY = 'admin_token';

function createAuthStore() {
	const { subscribe, set } = writable<boolean>(false);

	return {
		subscribe,
		init: async () => {
			if (!browser) return;

			const token = localStorage.getItem(TOKEN_KEY);
			if (!token) {
				set(false);
				return;
			}

			try {
				const response = await verifyToken(token);
				set(response.valid);
				if (!response.valid) {
					localStorage.removeItem(TOKEN_KEY);
				}
			} catch {
				localStorage.removeItem(TOKEN_KEY);
				set(false);
			}
		},
		login: async (password: string): Promise<boolean> => {
			try {
				const response = await apiLogin(password);
				if (browser) {
					localStorage.setItem(TOKEN_KEY, response.token);
				}
				set(true);
				return true;
			} catch {
				return false;
			}
		},
		logout: async () => {
			if (!browser) return;

			const token = localStorage.getItem(TOKEN_KEY);
			if (token) {
				try {
					await apiLogout(token);
				} catch {
					// Ignore errors during logout
				}
				localStorage.removeItem(TOKEN_KEY);
			}
			set(false);
		},
		getToken: (): string | null => {
			if (!browser) return null;
			return localStorage.getItem(TOKEN_KEY);
		}
	};
}

export const isAdmin = createAuthStore();
