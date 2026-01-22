/**
 * Pre-release tag priorities (higher = more stable)
 */
const PRE_RELEASE_PRIORITY: Record<string, number> = {
	rc: 80,
	'release-candidate': 80,
	releasecandidate: 80,
	beta: 60,
	alpha: 40,
	dev: 20,
	snapshot: 10
};

/**
 * Get priority and remainder for a pre-release tag.
 * Higher priority = more stable.
 */
function getPreReleasePriority(preRelease: string): { priority: number; remainder: string } {
	const lower = preRelease.toLowerCase();

	// Try to match known pre-release tags
	const match = lower.match(/^(rc|release[-.]?candidate|beta|alpha|dev|snapshot)(?:[-.]?(.*))?$/);

	if (!match) {
		// Unknown pre-release type - lowest priority
		return { priority: 0, remainder: preRelease };
	}

	const tag = match[1];
	const remainder = match[2] || '';
	const priority = PRE_RELEASE_PRIORITY[tag] ?? 0;

	return { priority, remainder };
}

/**
 * Compare pre-release remainders (e.g., "1" vs "2" in beta.1 vs beta.2)
 */
function comparePreReleaseRemainder(r1: string, r2: string): number {
	if (!r1 && !r2) return 0;
	if (!r1) return -1; // beta < beta.1
	if (!r2) return 1; // beta.1 > beta

	// Try numeric comparison
	const num1 = parseInt(r1, 10);
	const num2 = parseInt(r2, 10);

	if (!isNaN(num1) && !isNaN(num2)) {
		return num1 - num2;
	}

	// Fallback to string comparison
	return r1.localeCompare(r2, undefined, { sensitivity: 'base' });
}

/**
 * Compare pre-release tags with hierarchy:
 * release (no tag) > rc > beta > alpha > dev > snapshot > unknown
 */
function comparePreRelease(pr1: string | null, pr2: string | null): number {
	// No pre-release > has pre-release
	if (pr1 === null && pr2 === null) return 0;
	if (pr1 === null) return 1; // 1.0 > 1.0-anything
	if (pr2 === null) return -1; // 1.0-anything < 1.0

	// Both have pre-release, compare by priority
	const { priority: p1, remainder: r1 } = getPreReleasePriority(pr1);
	const { priority: p2, remainder: r2 } = getPreReleasePriority(pr2);

	if (p1 !== p2) {
		return p1 - p2;
	}

	// Same priority level, compare remainder
	return comparePreReleaseRemainder(r1, r2);
}

/**
 * Compare two version strings semantically.
 * Handles cases like "1.0" vs "1.0.0" (equal) and pre-release tags.
 *
 * Pre-release hierarchy (most stable to least):
 * release > rc > beta > alpha > dev > snapshot > unknown
 *
 * @returns negative if v1 < v2, positive if v1 > v2, 0 if equal
 */
export function compareVersions(v1: string | null | undefined, v2: string | null | undefined): number {
	if (!v1 && !v2) return 0;
	if (!v1) return -1;
	if (!v2) return 1;

	// Remove leading 'v' prefix
	const clean1 = v1.replace(/^v/i, '').trim();
	const clean2 = v2.replace(/^v/i, '').trim();

	// Split version and pre-release (e.g., "1.0.0-beta" -> ["1.0.0", "beta"])
	const [version1, preRelease1] = clean1.split('-', 2) as [string, string | undefined];
	const [version2, preRelease2] = clean2.split('-', 2) as [string, string | undefined];

	// Parse numeric segments
	const segments1 = version1.split('.').map((s) => parseInt(s, 10) || 0);
	const segments2 = version2.split('.').map((s) => parseInt(s, 10) || 0);

	// Compare segment by segment (missing segments treated as 0)
	const maxLength = Math.max(segments1.length, segments2.length);
	for (let i = 0; i < maxLength; i++) {
		const seg1 = segments1[i] ?? 0;
		const seg2 = segments2[i] ?? 0;
		if (seg1 !== seg2) {
			return seg1 - seg2;
		}
	}

	// Same version numbers - compare pre-release with hierarchy
	return comparePreRelease(preRelease1 ?? null, preRelease2 ?? null);
}

/**
 * Check if a newer version is available.
 * Returns true only if remote version is strictly greater than local.
 */
export function hasNewerVersion(
	localVersion: string | null | undefined,
	remoteVersion: string | null | undefined
): boolean {
	if (!localVersion || !remoteVersion) return false;
	return compareVersions(localVersion, remoteVersion) < 0;
}
