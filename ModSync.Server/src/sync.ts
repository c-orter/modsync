import type { VFS } from "@spt/utils/VFS";
import path from "node:path";
import { crc32Init, crc32Update, crc32Final } from "./crc";
import type { Config, SyncPath } from "./config";
import { HttpError, winPath } from "./utility";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import { createReadStream } from "node:fs";

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

	private async getFilesInDir(dir: string): Promise<[string, boolean][]> {
		if (!this.vfs.exists(dir)) {
			this.logger.warning(
				`Corter-ModSync: Directory '${dir}' does not exist, will be ignored.`,
			);
			return [];
		}

		const stats = await this.vfs.statPromisify(dir);
		if (stats.isFile())
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

		return (
			await Promise.all(
				this.vfs
					.getFiles(dir)
					.filter(
						(file) =>
							!file.endsWith(".nosync") && !file.endsWith(".nosync.txt"),
					)
					.map(
						async (file): Promise<[string, boolean]> => [
							path.join(dir, file),
							nosyncDir ||
								this.config.isExcluded(path.join(dir, file)) ||
								this.vfs.exists(`${path.join(dir, file)}.nosync`) ||
								this.vfs.exists(`${path.join(dir, file)}.nosync.txt`),
						],
					),
			)
		).concat(
			(
				await Promise.all(
					this.vfs
						.getDirs(dir)
						.map((subDir) => this.getFilesInDir(path.join(dir, subDir))),
				)
			)
				.flat()
				.map(([child, nosync]): [string, boolean] => [
					child,
					nosyncDir || nosync,
				]),
		);
	}

	private async buildModFile(
		file: string,
		// biome-ignore lint/correctness/noEmptyPattern: <explanation>
		{}: Required<SyncPath>,
		nosync: boolean,
	): Promise<ModFile> {
		try {
			return {
				nosync,
				crc: nosync ? 0 : await new Promise<number>((resolve, reject) => {
					let crc = crc32Init();

					createReadStream(file)
						.on("error", reject)
						.on("data", (data: Buffer) => {
							crc = crc32Update(crc, data);
						})
						.on("end", () => {
							resolve(crc32Final(crc));
						});
				}),
			};
		} catch (e) {
			throw new HttpError(500, `Corter-ModSync: Error reading '${file}'\n${e}`);
		}
	}

	public async hashModFiles(
		syncPaths: Config["syncPaths"],
	): Promise<Record<string, Record<string, ModFile>>> {
		return Object.fromEntries(
			await Promise.all(
				syncPaths.map(async (syncPath) => [
					winPath(syncPath.path),
					Object.fromEntries(
						await Promise.all(
							(await this.getFilesInDir(syncPath.path)).map(
								async ([file, nosync]) =>
									[
										winPath(file),
										await this.buildModFile(file, syncPath, nosync),
									] as const,
							),
						),
					),
				]),
			),
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
