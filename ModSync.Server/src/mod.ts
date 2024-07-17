import type { DependencyContainer } from "tsyringe";

import type { IncomingMessage, ServerResponse } from "node:http";
import path from "node:path";
import { lstatSync, watch } from "node:fs";
import crc32 from "buffer-crc32";
import type { IPreAkiLoadMod } from "@spt-aki/models/external/IPreAkiLoadMod";
import type { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import type { HttpListenerModService } from "@spt-aki/services/mod/httpListener/HttpListenerModService";
import type { HttpFileUtil } from "@spt-aki/utils/HttpFileUtil";
import type { VFS } from "@spt-aki/utils/VFS";
import type { JsonUtil } from "@spt-aki/utils/JsonUtil";
import { globNoEnd, globRegex } from "./glob";

type Config = { syncPaths: string[]; commonModExclusions: string[] };
type ModFile = { crc: number };

class Mod implements IPreAkiLoadMod {
	private static container: DependencyContainer;

	private static loadFailed = false;
	private static modFileHashes?: Record<string, ModFile>;
	private static config: Config;
	private static commonModExclusionsRegex: RegExp[];
	private static syncPathsUpdated = false;

	public preAkiLoad(container: DependencyContainer): void {
		Mod.container = container;
		const logger = container.resolve<ILogger>("WinstonLogger");
		const vfs = container.resolve<VFS>("VFS");
		const jsonUtil = container.resolve<JsonUtil>("JsonUtil");
		const httpListenerService = container.resolve<HttpListenerModService>(
			"HttpListenerModService",
		);
		httpListenerService.registerHttpListener(
			"ModSyncListener",
			this.canHandleOverride,
			this.handleOverride,
		);

		if (!vfs.exists(path.join(__dirname, "config.jsonc")))
			throw new Error(
				"Corter-ModSync: config.jsonc does not exist. Please verify your config is correct and try again.",
			);
			
		Mod.config = jsonUtil.deserializeJsonC(
			vfs.readFile(path.join(__dirname, "config.jsonc")),
			"config.jsonc",
		) as Config;

		if (
			Mod.config.syncPaths === undefined ||
			Mod.config.commonModExclusions === undefined
		) {
			Mod.loadFailed = true;
			throw new Error(
				"Corter-ModSync: One or more required config values is missing. Please verify your config is correct and try again.",
			);
		}

		Mod.commonModExclusionsRegex =
			Mod.config.commonModExclusions.map(globRegex);

		if (
			Mod.config.syncPaths === undefined ||
			Mod.config.syncPaths.length === 0
		) {
			logger.warning(
				"Corter-ModSync: No sync paths configured. Mod will not be loaded.",
			);
			Mod.loadFailed = true;
		}

		for (const syncPath of Mod.config.syncPaths) {
			if (!vfs.exists(syncPath)) {
				logger.warning(
					`Corter-ModSync: SyncPath '${syncPath}' does not exist. Path will not be synced.`,
				);
				continue;
			}

			watch(
				syncPath,
				{ recursive: true, persistent: false },
				async (e, filename) => {
					if (e === "rename") return;
					if (!filename) return;
					if (
						Mod.config.commonModExclusions.some((exclusion) =>
							globNoEnd(exclusion).test(path.join(syncPath, filename)
								.split(path.win32.sep)
								.join(path.posix.sep))
						)
					)
						return;
					if (vfs.exists(path.join(syncPath, filename)) && !lstatSync(path.join(syncPath, filename)).isFile()) return;

					if (!Mod.syncPathsUpdated) {
                        const updatedPath = path.join(
                            syncPath,
                            filename,
                        )
                            .split(path.win32.sep)
							.join(path.posix.sep);
                        
						logger.info(
							`Corter-ModSync: '${updatedPath}' was changed while the server is running, the hash cache will be recomputed on next request.`,
						);
						Mod.syncPathsUpdated = true;
					}
				},
			);
		}

		for (const syncPath in Mod.config.syncPaths) {
			if (path.isAbsolute(syncPath)) {
				throw new Error(
					`Corter-ModSync: SyncPaths must be relative to SPT server root. Invalid path '${syncPath}'`,
				);
			}

			if (
				path
					.relative(process.cwd(), path.resolve(process.cwd(), syncPath))
					.startsWith("..")
			) {
				throw new Error(
					`Corter-ModSync: SyncPaths must within SPT server root. Invalid path '${syncPath}'`,
				);
			}
		}
	}

	public canHandleOverride(_sessionId: string, req: IncomingMessage): boolean {
		return !Mod.loadFailed && (req.url?.startsWith("/modsync/") ?? false);
	}

	public async handleOverride(
		_sessionId: string,
		req: IncomingMessage,
		resp: ServerResponse,
	): Promise<void> {
		const logger = Mod.container.resolve<ILogger>("WinstonLogger");
		const vfs = Mod.container.resolve<VFS>("VFS");
		const httpFileUtil = Mod.container.resolve<HttpFileUtil>("HttpFileUtil");

		const getFileHashes = async (
			hashPaths: string[],
		): Promise<Record<string, ModFile>> => {
			const getFilesInDir = (dir: string): string[] => {
				try {
					return [
						...vfs
							.getFiles(dir)
							.map((file) => path.join(dir, file))
							.filter(
								(file) =>
									!file.endsWith(".nosync") &&
									!file.endsWith(".nosync.txt") &&
									!vfs.exists(`${file}.nosync`) &&
									!vfs.exists(`${file}.nosync.txt`) &&
									!Mod.commonModExclusionsRegex.some((exclusion) =>
										exclusion.test(
											path
												.relative(process.cwd(), file)
												.split(path.win32.sep)
												.join(path.posix.sep),
										),
									),
							),
						...vfs
							.getDirs(dir)
							.map((subDir) => path.join(dir, subDir))
							.filter(
								(subDir) =>
									!vfs.exists(path.join(subDir, ".nosync")) &&
									!vfs.exists(path.join(subDir, ".nosync.txt")) &&
									!Mod.commonModExclusionsRegex.some((exclusion) =>
										exclusion.test(
											path
												.relative(process.cwd(), subDir)
												.split(path.win32.sep)
												.join(path.posix.sep),
										),
									),
							)
							.flatMap((subDir) => getFilesInDir(subDir)),
					];
				} catch {
					return [];
				}
			};

			const buildModFile = async (file: string): Promise<[string, ModFile]> => {
				// biome-ignore lint/style/noNonNullAssertion: <explanation>
				const parent = hashPaths.find(
					(syncPath) => !path.relative(syncPath, file).startsWith(".."),
				)!;

				return [
					path
						.join(parent, path.relative(parent, file))
						.split(path.sep)
						.join(path.win32.sep),
					{
						crc: crc32.unsigned(vfs.readFile(file)),
					},
				];
			};

			const dirs = hashPaths.filter((syncPath) =>
				lstatSync(syncPath).isDirectory(),
			);

			const files = hashPaths.filter((syncPath) =>
				lstatSync(syncPath).isFile(),
			);

			return Object.fromEntries([
				...(await Promise.all(
					files.map((file) => path.join(process.cwd(), file)).map(buildModFile),
				)),
				...(await Promise.all(
					dirs
						.map((dir) => path.join(process.cwd(), dir))
						.flatMap((dir) => getFilesInDir(dir).map(buildModFile)),
				)),
			]);
		};

		const sanitizeFilePath = (file: string) => {
			const sanitizedPath = path.join(
				path.normalize(file).replace(/^(\.\.(\/|\\|$))+/, ""),
			);

			return (
				!Mod.config.syncPaths?.every((subDir) =>
					path
						.relative(path.join(process.cwd(), subDir), sanitizedPath)
						.startsWith(".."),
				) && sanitizedPath
			);
		};

		try {
			if (req.url === "/modsync/version") {
				const packageJson = JSON.parse(
					vfs.readFile(path.resolve(__dirname, "../package.json")),
				);

				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(packageJson.version));
			} else if (req.url === "/modsync/paths") {
				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(
					JSON.stringify(
						Mod.config.syncPaths.map((dir) =>
							dir.split(path.posix.sep).join(path.win32.sep),
						),
					),
				);
			} else if (req.url === "/modsync/hashes") {
				if (Mod.modFileHashes === undefined || Mod.syncPathsUpdated) {
					Mod.syncPathsUpdated = false;
					Mod.modFileHashes = await getFileHashes(
						Mod.config.syncPaths.filter(vfs.exists),
					);
				}

				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(Mod.modFileHashes));
			// biome-ignore lint/complexity/useOptionalChain: <explanation>
			} else if (req.url && req.url.startsWith("/modsync/fetch/")) {
				const filePath = decodeURIComponent(
					// biome-ignore lint/style/noNonNullAssertion: <explanation>
					req.url.split("/modsync/fetch/").at(-1)!,
				);

				const sanitizedPath = sanitizeFilePath(filePath);
				if (!sanitizedPath) {
					logger.warning(`Attempt to access invalid path ${filePath}`);
					resp.writeHead(400, "Bad request");
					resp.end("Invalid path");
					return;
				}

				if (!vfs.exists(sanitizedPath)) {
					logger.warning(`Attempt to access non-existent path ${filePath}`);
					resp.writeHead(404, "Not found");
					resp.end(`File ${filePath} not found`);
					return;
				}

				const fileStats = await vfs.statPromisify(sanitizedPath);
				resp.setHeader("Content-Length", fileStats.size);

				httpFileUtil.sendFile(resp, sanitizedPath);
			} else {
				logger.warning(`No route found for ${req.url}`);
				resp.writeHead(404, "Not found");
				resp.end(`No route found for ${req.url}`);
			}
		} catch (e) {
			if (e instanceof Error) {
				logger.error(`${e.message}\n${e.stack}`);
				resp.writeHead(500, "Internal server error");
				resp.end(e.toString());
			}
		}
	}
}

export const mod = new Mod();
