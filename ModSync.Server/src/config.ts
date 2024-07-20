import path from "node:path";
import type { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import type { JsonUtil } from "@spt/utils/JsonUtil";
import type { VFS } from "@spt/utils/VFS";
import { glob, globNoEnd } from "./glob";
import { unixPath } from "./utility";
import type { ILogger } from "@spt/models/spt/utils/ILogger";

export type SyncPath = {
	path: string;
	enabled?: boolean;
	// Not yet implemented
	// required?: boolean;
	// silent?: boolean;
};

type RawConfig = {
	syncPaths: (string | SyncPath)[];
	commonModExclusions: string[];
};

export class Config {
	constructor(
		public syncPaths: Required<SyncPath>[],
		public commonModExclusions: string[],
	) {}

	get enabledSyncPaths(): Required<SyncPath>[] {
		return this.syncPaths.filter(({ enabled }) => enabled);
	}

	public isExcluded(filePath: string): boolean {
		return this.commonModExclusions.some((exclusion) =>
			glob(exclusion).test(unixPath(filePath)),
		);
	}

	public isParentExcluded(filePath: string): boolean {
		return this.commonModExclusions.some((exclusion) =>
			globNoEnd(exclusion).test(unixPath(filePath)),
		);
	}
}
export class ConfigUtil {
	constructor(
		private vfs: VFS,
		private jsonUtil: JsonUtil,
		private modImporter: PreSptModLoader,
		private logger: ILogger,
	) {}

	/**
	 * @throws {Error} If the config file does not exist
	 */
	private readConfigFile(): RawConfig {
		const modPath = this.modImporter.getModPath("Corter-ModSync");
		const configPath = path.join(modPath, "src", "config.jsonc");

		return this.jsonUtil.deserializeJsonC(
			this.vfs.readFile(configPath),
			"config.jsonc",
		) as RawConfig;
	}

	/**
	 * @throws {Error} If the config is invalid
	 */
	private validateConfig(config: RawConfig): void {
		if (!Array.isArray(config.syncPaths))
			throw new Error(
				"Corter-ModSync: config.jsonc 'syncPaths' is not an array. Please verify your config is correct and try again.",
			);

		if (!Array.isArray(config.commonModExclusions))
			throw new Error(
				"Corter-ModSync: config.jsonc 'commonModExclusions' is not an array. Please verify your config is correct and try again.",
			);

		for (const syncPath of config.syncPaths) {
			if (typeof syncPath !== "string" && !("path" in syncPath))
				throw new Error(
					"Corter-ModSync: config.jsonc 'syncPaths' is missing 'path'. Please verify your config is correct and try again.",
				);

			if (
				typeof syncPath === "string"
					? path.isAbsolute(syncPath)
					: path.isAbsolute(syncPath.path)
			)
				throw new Error(
					`Corter-ModSync: SyncPaths must be relative to SPT server root. Invalid path '${syncPath}'`,
				);

			if (
				path
					.relative(
						process.cwd(),
						path.resolve(
							process.cwd(),
							typeof syncPath === "string" ? syncPath : syncPath.path,
						),
					)
					.startsWith("..")
			)
				throw new Error(
					`Corter-ModSync: SyncPaths must within SPT server root. Invalid path '${syncPath}'`,
				);
		}
	}

	public load(): Config {
		const rawConfig = this.readConfigFile();
		this.validateConfig(rawConfig);

		return new Config(
			rawConfig.syncPaths
				.map((syncPath) => ({
					enabled: true,
					// Not yet implemented
					// force: false,
					// silent: false,
					...(typeof syncPath === "string" ? { path: syncPath } : syncPath),
				}))
				.filter(({ path: syncPath }) => {
					const exists = this.vfs.exists(path.join(process.cwd(), syncPath));
					if (!exists)
						this.logger.warning(
							`Corter-ModSync: SyncPath '${syncPath}' does not exist, will be ignored.`,
						);
					return exists;
				}),
			rawConfig.commonModExclusions,
		);
	}
}
