import type { DependencyContainer } from "tsyringe";

import type { IncomingMessage, ServerResponse } from "node:http";
import path from "node:path";
import fs from "node:fs";
import type { IPreAkiLoadMod } from "@spt-aki/models/external/IPreAkiLoadMod";
import type { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import type { HttpListenerModService } from "@spt-aki/services/mod/httpListener/HttpListenerModService";
import type { HashUtil } from "@spt-aki/utils/HashUtil";
import type { HttpFileUtil } from "@spt-aki/utils/HttpFileUtil";
import type { VFS } from "@spt-aki/utils/VFS";

class Mod implements IPreAkiLoadMod {
	private static container: DependencyContainer;

	private clientModLastUpdated = 0;
	private clientModHashes?: Record<string, { crc: number; modified: number }>;
	private serverModHashes?: Record<string, { crc: number; modified: number }>;

	public preAkiLoad(container: DependencyContainer): void {
		Mod.container = container;
		const httpListenerService = container.resolve<HttpListenerModService>(
			"HttpListenerModService",
		);
		httpListenerService.registerHttpListener(
			"ModSyncListener",
			this.canHandleOverride,
			this.handleOverride,
		);
	}

	public canHandleOverride(_sessionId: string, req: IncomingMessage): boolean {
		return req.url?.startsWith("/modsync/") ?? false;
	}

	public async handleOverride(
		_sessionId: string,
		req: IncomingMessage,
		resp: ServerResponse,
	): Promise<void> {
		const logger = Mod.container.resolve<ILogger>("WinstonLogger");
		const vfs = Mod.container.resolve<VFS>("VFS");
		const hashUtil = Mod.container.resolve<HashUtil>("HashUtil");
		const httpFileUtil = Mod.container.resolve<HttpFileUtil>("HttpFileUtil");

		const {
			clientDirs,
			serverDirs,
		}: {
			clientDirs: string[];
			serverDirs: string[];
		} = require("./config.json");

		if (
			clientDirs.some(
				(dir) =>
					path.isAbsolute(dir) ||
					path
						.relative(process.cwd(), path.resolve(process.cwd(), dir))
						.startsWith(".."),
			)
		)
			logger.error(
				"Invalid clientDirs in config.json. Ensure directories are relative to the SPT server directory (ie. BepInEx/plugins)",
			);

		if (
			serverDirs.some(
				(dir) =>
					path.isAbsolute(dir) ||
					path
						.relative(process.cwd(), path.resolve(process.cwd(), dir))
						.startsWith(".."),
			)
		)
			logger.error(
				"Invalid serverDirs in config.json. Ensure directories are relative to the SPT server directory (ie. user/mods)",
			);

		const getFileHashes = async (
			dirs: string[],
		): Promise<Record<string, { crc: number; modified: number }>> => {
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
									path.basename(file) !== "Corter-ModSync.dll",
							),
						...vfs
							.getDirs(dir)
							.filter(
								(subDir) =>
									!vfs.exists(path.join(dir, subDir, ".nosync")) &&
									!vfs.exists(path.join(dir, subDir, ".nosync.txt")),
							)
							.flatMap((subDir) => getFilesInDir(path.join(dir, subDir))),
					];
				} catch {
					return [];
				}
			};

			const buildModFile = (file: string) => {
				const modified = fs.statSync(file).mtime;

				// biome-ignore lint/style/noNonNullAssertion: <explanation>
				const dir = dirs.find(
					(dir) => !path.relative(dir, file).startsWith(".."),
				)!;

				return [
					path.join(dir, path.relative(dir, file)),
					{
						crc: hashUtil.generateCRC32ForFile(file),
						modified: new Date(
							modified.getTime() + modified.getTimezoneOffset() * 60000,
						).getTime(),
					},
				];
			};

			return Object.assign(
				{},
				...dirs
					.map((dir) => path.join(process.cwd(), dir))
					.map((dir) =>
						Object.fromEntries(getFilesInDir(dir).map(buildModFile)),
					),
			);
		};

		const sanitizeFilePath = (file: string, allowedSubDirs: string[]) => {
			const sanitizedPath = path.join(
				path.normalize(file).replace(/^(\.\.(\/|\\|$))+/, ""),
			);

			return (
				!allowedSubDirs.every((subDir) =>
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
				resp.end(JSON.stringify({ version: packageJson.version }));
			} else if (req.url === "/modsync/client/dirs") {
				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(clientDirs));
			} else if (req.url === "/modsync/server/dirs") {
				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(serverDirs));
			} else if (req.url === "/modsync/client/hashes") {
				const clientModUpdated = Math.max(
					...clientDirs.map((dir) => fs.statSync(dir).mtimeMs),
				);

				if (
					this.clientModHashes === undefined ||
					clientModUpdated > this.clientModLastUpdated
				) {
					this.clientModLastUpdated = clientModUpdated;

					this.clientModHashes = await getFileHashes(clientDirs);
				}

				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(this.clientModHashes));
			} else if (req.url === "/modsync/server/hashes") {
				if (this.serverModHashes === undefined) {
					this.serverModHashes = await getFileHashes(serverDirs);
				}

				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(this.serverModHashes));
			} else if (req.url?.startsWith("/modsync/client/fetch/")) {
				const filePath = decodeURIComponent(
					// biome-ignore lint/style/noNonNullAssertion: <explanation>
					req.url.split("/modsync/client/fetch/").at(-1)!,
				);

				const sanitizedPath = sanitizeFilePath(filePath, clientDirs);
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

				httpFileUtil.sendFile(resp, sanitizedPath);
			} else if (req.url?.startsWith("/modsync/server/fetch/")) {
				const filePath = decodeURIComponent(
					// biome-ignore lint/style/noNonNullAssertion: <explanation>
					req.url.split("/modsync/server/fetch/").at(-1)!,
				);
				const sanitizedPath = sanitizeFilePath(filePath, serverDirs);
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

				httpFileUtil.sendFile(resp, sanitizedPath);
			} else {
				logger.warning(`No route found for ${req.url}`);
				resp.writeHead(404, "Not found");
				resp.end(`No route found for ${req.url}`);
			}
		} catch (e) {
			if (e instanceof Error) {
				logger.error(e.toString());
				resp.writeHead(500, "Internal server error");
				resp.end(e.toString());
			}
		}
	}
}

export const mod = new Mod();
