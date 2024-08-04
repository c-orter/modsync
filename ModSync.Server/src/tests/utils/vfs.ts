import { fs } from "memfs";
import type Dirent from "memfs/lib/Dirent";
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
	public readFile(path: string) {
		const contents = fs.readFileSync(path);

		if (contents instanceof Buffer) return contents.toString();

		return contents;
	}
}
