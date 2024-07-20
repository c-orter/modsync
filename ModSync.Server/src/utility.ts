import path from "node:path";

export class HttpError extends Error {
	constructor(
		public code: number,
		message: string,
	) {
		super(message);
	}

	get codeMessage(): string {
		switch (this.code) {
			case 400:
				return "Bad Request";
			case 404:
				return "Not Found";
			default:
				return "Internal Server Error";
		}
	}
}

export function winPath(p: string): string {
	return p.split(path.posix.sep).join(path.win32.sep);
}

export function unixPath(p: string): string {
	return p.split(path.win32.sep).join(path.posix.sep);
}
