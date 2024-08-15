import { fs } from "memfs";
import type Dirent from "memfs/lib/Dirent";
import type { StatOptions, Stats } from "node:fs";
export class VFS {
	public exists(path: string) {
		return fs.existsSync(path);
	}

	public getFiles(path: string) {
		return (fs.readdirSync(path, { withFileTypes: true }) as Dirent[])
			.filter((item) => !item.isDirectory())
			.map((item) => item.name);
	}
	public getDirs(path: string) {
		return (fs.readdirSync(path, { withFileTypes: true }) as Dirent[])
			.filter((item) => item.isDirectory())
			.map((item) => item.name);
	}
	public readFilePromisify(path: string): Promise<Buffer> {
		const contents = fs.readFileSync(path);

		return Promise.resolve(contents as Buffer);
	}

	public writeFilePromisify(
		path: string,
		contents: string,
		options?: Record<string, string | number>,
	) {
		return fs.writeFileSync(path, contents);
	}

	public statPromisify(
		path: string,
		options?: StatOptions & { bigint?: false },
	) {
		return Promise.resolve(fs.statSync(path) as Stats);
	}
}
