import path from "node:path";
import type { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import type { JsonUtil } from "@spt/utils/JsonUtil";
import type { VFS } from "@spt/utils/VFS";
import { glob, globNoEnd } from "./glob";
import { unixPath } from "./utility";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import fs from "node:fs";

export type SyncPath = {
	path: string;
	enabled?: boolean;
	enforced?: boolean;
	silent?: boolean;
};

type RawConfig = {
	syncPaths: (string | SyncPath)[];
	commonModExclusions: string[];
};

const DEFAULT_CONFIG = `{
	"syncPaths": [
		"BepInEx/plugins",
		"BepInEx/patchers",
		"BepInEx/config",
		{
			"enabled": false,
			"path": "user/mods"
		},
		// ModSync Updater - is copied into position immediately upon update
		"ModSync.Updater.exe"
	],
	"commonModExclusions": [
		// SPT Installer
		"BepInEx/plugins/spt",
		"BepInEx/patchers/spt-prepatch.dll",
		// Questing Bots
		"BepInEx/plugins/DanW-SPTQuestingBots/log",
		// Realism
		"user/mods/SPT-Realism/ProfileBackups",
		// Fika
		"user/mods/fika-server/types",
		"user/mods/fika-server/cache",
		"BepInEx/plugins/Fika.Dedicated.dll",
		// Live Flea Prices
		"user/mods/DrakiaXYZ-LiveFleaPrices/config",
		// Questing Bots
		"BepInEx/plugins/DanW-SPTQuestingBots/log",
		// EFTApi
		"BepInEx/plugins/kmyuhkyuk-EFTApi/cache",
		// Expanded Task Text (Accounts for bug with current version)
		"user/mods/ExpandedTaskText/src/**/cache.json",
		// Leaves Loot Fuckery
		"user/mods/leaves-loot_fuckery/output",
		// ADD MISSING QUEST WEAPON REQUIREMENTS
		"user/mods/zz_guiltyman-addmissingquestweaponrequirements/log.log",
		"user/mods/zz_guiltyman-addmissingquestweaponrequirements/user/logs",
		// General server mods
		"user/mods/**/.git",
		"user/mods/**/node_modules",
		"user/mods/**/*.js",
		"user/mods/**/*.js.map"
	]
}`;

export class Config {
	constructor(
		public syncPaths: Required<SyncPath>[],
		public commonModExclusions: string[],
	) {}

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
		const configPath = path.join(modPath, "config.jsonc");

		if (!this.vfs.exists(configPath))
			fs.writeFileSync(configPath, DEFAULT_CONFIG);

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
			rawConfig.syncPaths.map((syncPath) => ({
				enabled: true,
				// Not yet implemented
				enforced: false,
				silent: false,
				restartRequired: true,
				...(typeof syncPath === "string" ? { path: syncPath } : syncPath),
			})),
			rawConfig.commonModExclusions,
		);
	}
}
