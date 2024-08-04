import type { HttpFileUtil } from "@spt/utils/HttpFileUtil";
import type { SyncUtil } from "./sync";
import { glob } from "./glob";
import type { IncomingMessage, ServerResponse } from "node:http";
import path from "node:path";
import type { VFS } from "@spt/utils/VFS";
import type { Config } from "./config";
import { HttpError, winPath } from "./utility";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import type { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import { statSync } from "node:fs";

export class Router {
	constructor(
		private config: Config,
		private syncUtil: SyncUtil,
		private vfs: VFS,
		private httpFileUtil: HttpFileUtil,
		private modImporter: PreSptModLoader,
		private logger: ILogger,
	) {}

	/**
	 * @internal
	 */
	public getServerVersion(res: ServerResponse, _: RegExpMatchArray) {
		const modPath = this.modImporter.getModPath("Corter-ModSync");
		const packageJson = JSON.parse(
			this.vfs.readFile(path.join(modPath, "package.json")),
		);

		res.setHeader("Content-Type", "application/json");
		res.writeHead(200, "OK");
		res.end(JSON.stringify(packageJson.version));
	}

	/**
	 * @internal
	 */
	public getSyncPaths(res: ServerResponse, _: RegExpMatchArray) {
		res.setHeader("Content-Type", "application/json");
		res.writeHead(200, "OK");
		res.end(
			JSON.stringify(
				this.config.syncPaths.map(({ path, ...rest }) => ({
					path: winPath(path),
					...rest,
				})),
			),
		);
	}

	/**
	 * @internal
	 */
	public getHashes(res: ServerResponse, _: RegExpMatchArray) {
		res.setHeader("Content-Type", "application/json");
		res.writeHead(200, "OK");
		res.end(
			JSON.stringify(this.syncUtil.hashModFiles(this.config.syncPaths)),
		);
	}

	/**
	 * @internal
	 */
	public fetchModFile(res: ServerResponse, matches: RegExpMatchArray) {
		const filePath = decodeURIComponent(matches[1]);

		const sanitizedPath = this.syncUtil.sanitizeDownloadPath(
			filePath,
			this.config.syncPaths,
		);

		if (!this.vfs.exists(sanitizedPath))
			throw new HttpError(
				404,
				`Attempt to access non-existent path ${filePath}`,
			);

		try {
			const fileStats = statSync(sanitizedPath);
			res.setHeader("Content-Length", fileStats.size);
			this.httpFileUtil.sendFile(res, sanitizedPath);
		} catch (e) {
			throw new HttpError(
				500,
				`Corter-ModSync: Error reading '${filePath}'\n${e}`,
			);
		}
	}

	public handleRequest(req: IncomingMessage, res: ServerResponse) {
		const routeTable = [
			{
				route: glob("/modsync/version"),
				handler: this.getServerVersion.bind(this),
			},
			{
				route: glob("/modsync/paths"),
				handler: this.getSyncPaths.bind(this),
			},
			{
				route: glob("/modsync/hashes"),
				handler: this.getHashes.bind(this),
			},
			{
				route: glob("/modsync/fetch/**"),
				handler: this.fetchModFile.bind(this),
			},
		];

		try {
			for (const { route, handler } of routeTable) {
				const matches = route.exec(req.url || "");
				if (matches) return handler(res, matches);
			}

			throw new HttpError(404, "Corter-ModSync: Unknown route");
		} catch (e) {
			this.logger.error(
				`Corter-ModSync: Error when handling [${req.method} ${req.url}]:\n${e}`,
			);

			if (e instanceof HttpError) {
				res.writeHead(e.code, e.codeMessage);
				res.end(e.message);
			} else {
				res.writeHead(500, "Internal server error");
				res.end(
					`Corter-ModSync: Error handling [${req.method} ${req.url}]:\n${e}`,
				);
			}
		}
	}
}
