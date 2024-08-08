import type { VFS } from "@spt/utils/VFS";
import path from "node:path";
import crc32 from "buffer-crc32";
import type { Config, SyncPath } from "./config";
import { HttpError, winPath } from "./utility";
import { readFileSync, statSync } from "node:fs";
import type { ILogger } from "@spt/models/spt/utils/ILogger";

type ModFile = {
	crc: number;
	nosync: boolean;
};

export class SyncUtil {
	constructor(
		private vfs: VFS,
		private config: Config,
		private logger: ILogger,
	) {}

	private getFilesInDir(dir: string): [string, boolean][] {
		if (!this.vfs.exists(dir)) {
			this.logger.warning(
				`Corter-ModSync: Directory '${dir}' does not exist, will be ignored.`,
			);
			return [];
		}
		if (statSync(dir).isFile())
			return [
				[
					dir,
					this.config.isExcluded(dir) ||
						this.vfs.exists(path.join(dir, ".nosync")) ||
						this.vfs.exists(path.join(dir, ".nosync.txt")),
				],
			];

		const nosyncDir =
			this.config.isExcluded(dir) ||
			this.vfs.exists(path.join(dir, ".nosync")) ||
			this.vfs.exists(path.join(dir, ".nosync.txt"));

		return this.vfs
			.getFiles(dir)
			.filter(
				(file) => !file.endsWith(".nosync") && !file.endsWith(".nosync.txt"),
			)
			.map((file): [string, boolean] => [
				path.join(dir, file),
				nosyncDir ||
					this.config.isExcluded(path.join(dir, file)) ||
					this.vfs.exists(`${path.join(dir, file)}.nosync`) ||
					this.vfs.exists(`${path.join(dir, file)}.nosync.txt`),
			])
			.concat(
				this.vfs
					.getDirs(dir)
					.flatMap((subDir) => this.getFilesInDir(path.join(dir, subDir)))
					.map(([child, nosync]): [string, boolean] => [
						child,
						nosyncDir || nosync,
					]),
			);
	}

	private buildModFile(
		file: string,
		// biome-ignore lint/correctness/noEmptyPattern: <explanation>
		{}: Required<SyncPath>,
		nosync: boolean,
	): ModFile {
		try {
			return {
				nosync,
				crc: nosync ? 0 : crc32.unsigned(readFileSync(file)),
			};
		} catch (e) {
			throw new HttpError(500, `Corter-ModSync: Error reading '${file}'\n${e}`);
		}
	}

	public hashModFiles(
		syncPaths: Config["syncPaths"],
	): Record<string, Record<string, ModFile>> {
		return Object.fromEntries(
			syncPaths.map((syncPath) => [
				winPath(syncPath.path),
				Object.fromEntries(
					this.getFilesInDir(syncPath.path).map(
						([file, nosync]) =>
							[
								winPath(file),
								this.buildModFile(file, syncPath, nosync),
							] as const,
					),
				),
			]),
		);
	}

	/**
	 * @throws {Error} If file path is invalid
	 */
	public sanitizeDownloadPath(
		file: string,
		syncPaths: Config["syncPaths"],
	): string {
		const normalized = path.join(
			path.normalize(file).replace(/^(\.\.(\/|\\|$))+/, ""),
		);

		if (
			!syncPaths.some(
				({ path: p }) =>
					!path
						.relative(path.join(process.cwd(), p), normalized)
						.startsWith(".."),
			)
		)
			throw new HttpError(
				400,
				`Corter-ModSync: Requested file '${file}' is not in an enabled sync path!`,
			);

		return normalized;
	}
}
