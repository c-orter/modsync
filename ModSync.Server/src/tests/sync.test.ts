import { expect, describe, it, vi, beforeEach } from "vitest";
import { fs, vol } from "memfs";

import { SyncUtil } from "../sync";
import { Config } from "../config";
import { VFS } from "./utils/vfs";
import type { VFS as IVFS } from "@spt/utils/VFS";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import { mock } from "vitest-mock-extended";

vi.mock("node:fs", () => ({ ...fs, default: fs }));

const directoryStructure = {
	plugins: {
		"file1.dll": "Test content",
		"file2.dll": "Test content 2",
		"file2.dll.nosync": "",
		"file3.dll": "Test content 3",
		"file3.dll.nosync.txt": "",
		ModName: {
			"mod_name.dll": "Test content 4",
			".nosync": "",
		},
		OtherMod: {
			"other_mod.dll": "Test content 5",
			subdir: {
				"image.png": "Test Image",
				".nosync": "",
			},
		},
	},
	user: {
		mods: {
			testMod: {
				"test.ts": "Test content 6",
				"test.js": "Test content 6",
				"test.js.map": "Test content 7",
			},
		},
	},
};

const config = new Config(
	[{ path: "plugins", enabled: true /* silent: false, required: false */ }],
	[],
);

const vfs = new VFS();
const logger = mock<ILogger>();
const syncUtil = new SyncUtil(vfs as IVFS, config, logger);

beforeEach(() => {
	vol.reset();
	vol.fromNestedJSON(directoryStructure);
});

describe("hashModFiles", () => {
	it("should hash mod files", () => {
		const hashes = syncUtil.hashModFiles(config.syncPaths);

		expect(
			Object.keys(hashes).some(
				(file) => file.endsWith(".nosync") || file.endsWith(".nosync.txt"),
			),
		).toBe(false);

		expect("plugins\\OtherMod\\other_mod.dll" in hashes).toBe(true);

		expect(hashes).toMatchSnapshot();
	});

	it("should correctly hash multiple folders", () => {
		const config = new Config(
			[
				{ path: "plugins", enabled: true },
				{
					path: "user/mods",
					enabled: true,
				},
			],
			["user/mods/**/*.js", "user/mods/**/*.js.map"],
		);

		const vfs = new VFS();
		const syncUtil = new SyncUtil(vfs as IVFS, config, logger);

		const hashes = syncUtil.hashModFiles(config.syncPaths);

		expect(hashes).toMatchSnapshot();
	});

	it("should correctly ignore folders that do not exist", () => {
		const config = new Config(
			[
				{ path: "plugins", enabled: true },
				{
					path: "user/bananas",
					enabled: true,
				},
			],
			["user/mods/**/*.js", "user/mods/**/*.js.map"],
		);

		const vfs = new VFS();
		const syncUtil = new SyncUtil(vfs as IVFS, config, logger);

		const hashes = syncUtil.hashModFiles(config.syncPaths);

		expect(Object.keys(hashes)).toContain("plugins\\file1.dll");
		expect(Object.keys(hashes)).toContain("plugins\\OtherMod\\other_mod.dll");
		expect(logger.warning).toHaveBeenCalledWith(
			"Corter-ModSync: Directory 'user/bananas' does not exist, will be ignored.",
		);
	});
	
	it("should correctly hash folders that didn't exist initially but are created", () => {
		const config = new Config(
			[
				{path: "plugins", enabled: true},
				{
					path: "user/bananas",
					enabled: true,
				},
			],
			["user/mods/**/*.js", "user/mods/**/*.js.map"],
		);

		const vfs = new VFS();
		const syncUtil = new SyncUtil(vfs as IVFS, config, logger);
		
		const hashes = syncUtil.hashModFiles(config.syncPaths);
		
		expect(Object.keys(hashes)).toContain("plugins\\file1.dll");
		expect(Object.keys(hashes)).toContain("plugins\\OtherMod\\other_mod.dll");
		
		expect(logger.warning).toHaveBeenCalledWith(
			"Corter-ModSync: Directory 'user/bananas' does not exist, will be ignored.",
		);
		
		fs.mkdirSync("user/bananas", {recursive: true});
		fs.writeFileSync("user/bananas/test.txt", "test");
		
		const newHashes = syncUtil.hashModFiles(config.syncPaths);
		
		expect(newHashes).toMatchSnapshot()
	})
});

describe("sanitizeDownloadPath", () => {
	it("should sanitize correct download paths", () => {
		expect(
			syncUtil.sanitizeDownloadPath("plugins\\file1.dll", config.syncPaths),
		).toBe("plugins\\file1.dll");
	});

	it("should throw for download paths outside SPT root", () => {
		expect(() => {
			syncUtil.sanitizeDownloadPath("plugins\\..\\file1.dll", config.syncPaths);
		}).toThrow();
	});

	it("should throw for files not in syncPath", () => {
		expect(() => {
			syncUtil.sanitizeDownloadPath("otherDir\\file1.dll", config.syncPaths);
		}).toThrow();
	});
});
