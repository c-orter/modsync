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

	private version!: string;

	private clientModHashes?: Record<string, { crc: number; modified: number }>;
	private serverModHashes?: Record<string, { crc: number; modified: number }>;

	public preAkiLoad(container: DependencyContainer): void {
		Mod.container = container;
		const logger = Mod.container.resolve<ILogger>("WinstonLogger");
		const vfs = Mod.container.resolve<VFS>("VFS");

		const packageJson = JSON.parse(
			vfs.readFile(path.resolve(__dirname, "../package.json")),
		);
		this.version = packageJson.version;

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

		const getFileHashes = async (
			baseDir: string,
			dirs: string[],
		): Promise<Record<string, { crc: number; modified: number }>> => {
			const basePath = path.resolve(process.cwd(), baseDir);

			const getFilesInDir = (dir: string): string[] => {
				try {
					return [
						...vfs
							.getFiles(dir)
							.map((file) => path.join(dir, file))
							.filter(
								(file) =>
									!file.endsWith(".nosync") && !vfs.exists(`${file}.nosync`),
							),
						...vfs
							.getDirs(dir)
							.filter((dir) => !vfs.exists(path.join(dir, ".nosync")))
							.flatMap((subDir) => getFilesInDir(path.join(dir, subDir))),
					];
				} catch {
					return [];
				}
			};

			const buildModFile = (file: string) => {
				const modified = fs.statSync(file).mtime;
				return [
					path
						.relative(basePath, file)
						.split(path.posix.sep)
						.join(path.win32.sep),
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
				...dirs.map((dir) => {
					const subDir = path.join(baseDir, dir);
					const subDirFiles = getFilesInDir(subDir);

					return Object.fromEntries(subDirFiles.map(buildModFile));
				}),
			);
		};

		const sanitizeFilePath = (
			file: string,
			baseDir: string,
			allowedSubDirs: string[],
		) => {
			const sanitizedPath = path.join(
				baseDir,
				path.normalize(file).replace(/^(\.\.(\/|\\|$))+/, ""),
			);

			return (
				!allowedSubDirs.every((subDir) =>
					path
						.relative(path.join(baseDir, subDir), sanitizedPath)
						.startsWith(".."),
				) && sanitizedPath
			);
		};

		try {
			if (req.url === "/modsync/version") {
				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify({ version: this.version }));
			} else if (req.url === "/modsync/client/hashes") {
				if (this.clientModHashes === undefined) {
					this.clientModHashes = await getFileHashes("BepInEx", [
						"plugins",
						"config",
					]);
				}

				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(this.clientModHashes));
			} else if (req.url === "/modsync/server/hashes") {
				if (this.serverModHashes === undefined) {
					this.serverModHashes = await getFileHashes("user", ["mods"]);
				}

				resp.setHeader("Content-Type", "application/json");
				resp.writeHead(200, "OK");
				resp.end(JSON.stringify(this.serverModHashes));
			} else if (req.url?.startsWith("/modsync/client/fetch/")) {
				const filePath = decodeURIComponent(
					// biome-ignore lint/style/noNonNullAssertion: <explanation>
					req.url.split("/modsync/client/fetch/").at(-1)!,
				);

				const sanitizedPath = sanitizeFilePath(filePath, "BepInEx", [
					"plugins",
					"config",
				]);
				if (!sanitizedPath) {
					logger.warning(`Attempt to access invalid path ${filePath}`);
					resp.writeHead(400, "Bad request");
					resp.end("Invalid path");
					return;
				}

				httpFileUtil.sendFile(resp, sanitizedPath);
			} else if (req.url?.startsWith("/modsync/server/fetch/")) {
				const filePath = decodeURIComponent(
					// biome-ignore lint/style/noNonNullAssertion: <explanation>
					req.url.split("/modsync/server/fetch/").at(-1)!,
				);
				const sanitizedPath = sanitizeFilePath(filePath, "user", ["mods"]);
				if (!sanitizedPath) {
					logger.warning(`Attempt to access invalid path ${filePath}`);
					resp.writeHead(400, "Bad request");
					resp.end("Invalid path");
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
