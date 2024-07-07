// Full Credit to https://github.com/aleclarson/glob-regex

const dotRE = /\./g;
const dotPattern = "\\.";

const restRE = /\*\*$/g;
const restPattern = "(.+)";

// noinspection RegExpUnnecessaryNonCapturingGroup
const globRE = /(?:\*\*\/|\*\*|\*)/g;
const globPatterns: Record<string, string> = {
	"*": "([^/]+)", // no backslashes
	"**": "(.+/)?([^/]+)", // short for "**/*"
	"**/": "(.+/)?", // one or more directories
};

function mapToPattern(str: string) {
	return globPatterns[str];
}

function replace(glob: string) {
	return glob
		.replace(dotRE, dotPattern)
		.replace(restRE, restPattern)
		.replace(globRE, mapToPattern);
}

function join(globs: string[]) {
	return `((${globs.map(replace).join(")|(")}))`;
}

export function globRegex(glob: string | string[]) {
	return new RegExp(`^${Array.isArray(glob) ? join(glob) : replace(glob)}$`);
}