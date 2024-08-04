import type { VFS } from "@spt/utils/VFS";
import path from "node:path";
import crc32 from "buffer-crc32";
import type { Config, SyncPath } from "./config";
import { HttpError, winPath } from "./utility";
import fs from "node:fs";
import type { ILogger } from "@spt/models/spt/utils/ILogger";

type ModFile = {
	crc: number;
	// Not yet implemented
	// required: boolean;
	// silent: boolean
};

export class SyncUtil {
	constructor(
		private vfs: VFS,
		private config: Config,
		private logger: ILogger,
	) {}

	private getFilesInDir(dir: string): string[] {
		if (!this.vfs.exists(dir)) {
			this.logger.warning(
				`Corter-ModSync: Directory '${dir}' does not exist, will be ignored.`,
			);
			return [];
		}
		if (fs.statSync(dir).isFile()) return [dir];

		if (
			this.vfs.exists(path.join(dir, ".nosync")) ||
			this.vfs.exists(path.join(dir, ".nosync.txt"))
		)
			return [];

		if (this.config.isExcluded(dir)) return [];

		return this.vfs
			.getFiles(dir)
			.filter((file) => !this.config.isExcluded(path.join(dir, file)))
			.filter(
				(file) => !file.endsWith(".nosync") && !file.endsWith(".nosync.txt"),
			)
			.filter(
				(file) =>
					!this.vfs.exists(`${path.join(dir, file)}.nosync`) &&
					!this.vfs.exists(`${path.join(dir, file)}.nosync.txt`),
			)
			.map((file) => path.join(dir, file))
			.concat(
				this.vfs
					.getDirs(dir)
					.flatMap((subDir) => this.getFilesInDir(path.join(dir, subDir))),
			);
	}

	private buildModFile(
		file: string,
		// Not yet implemented
		// biome-ignore lint/correctness/noEmptyPattern: <explanation>
		{
			/* required, silent */
			/* required, silent */
		}: Required<SyncPath>,
	): ModFile {
		try {
			return {
				crc: crc32.unsigned(fs.readFileSync(file)),
				// Not yet implemented
				// required,
				// silent,
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
						(file) =>
							[winPath(file), this.buildModFile(file, syncPath)] as const,
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
