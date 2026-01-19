export interface Mod {
	name: string;
	fileName: string;
	version: string;
	description?: string;
	authors: string[];
	website?: string;
	curseForgeUrl?: string;
	foundVia?: string;
}

export interface ModListResponse {
	lastUpdated: string | null;
	totalCount: number;
	mods: Mod[];
}

export interface RefreshProgress {
	processed: number;
	total: number;
	currentMod?: string;
}

export interface StatusResponse {
	lastUpdated?: string;
	modCount: number;
	isRefreshing: boolean;
	progress?: RefreshProgress;
	nextScheduledRefresh?: string;
}
