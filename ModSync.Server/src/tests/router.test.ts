import { expect, beforeEach, describe, it, vi } from "vitest";
import { fs, vol } from "memfs";

import { Router } from "../router";
import { Config } from "../config";
import { SyncUtil } from "../sync";
import { VFS } from "./utils/vfs";
import type { VFS as IVFS } from "@spt/utils/VFS";
import type { HttpFileUtil } from "@spt/utils/HttpFileUtil";
import { mock } from "vitest-mock-extended";
import { PreSptModLoader } from "./utils/preSptModLoader";
import type { PreSptModLoader as IPreSptModLoader } from "@spt/loaders/PreSptModLoader";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import type { ServerResponse } from "node:http";
import { HttpError } from "../utility";

vi.mock("node:fs", () => ({ ...fs, default: fs }));

describe("router", () => {
	const config = new Config(
		[
			{
				path: "plugins",
				enabled: true,
				enforced: false,
				silent: false,
				restartRequired: true,
			},
			{
				path: "user/mods",
				enabled: true,
				enforced: false,
				silent: false,
				restartRequired: false,
			},
			{
				path: "user/cache",
				enabled: false,
				enforced: false,
				silent: false,
				restartRequired: false,
			},
		],
		["plugins/**/node_modules"],
	);
	const vfs = new VFS() as IVFS;
	const logger = mock<ILogger>();
	const syncUtil = new SyncUtil(vfs, config, logger);
	const httpFileUtil = mock<HttpFileUtil>();
	const modImporter = new PreSptModLoader() as IPreSptModLoader;
	const router = new Router(
		config,
		syncUtil,
		vfs,
		httpFileUtil,
		modImporter,
		logger,
	);

	describe("getServerVersion", () => {
		let res: ServerResponse;
		beforeEach(() => {
			vol.reset();
			vol.fromNestedJSON({ "package.json": '{ "version": "1.0.0" }' });

			res = mock<ServerResponse>();
		});

		it("should return server version", () => {
			router.getServerVersion(res, mock<RegExpMatchArray>());

			expect(res.end).toHaveBeenCalledWith(JSON.stringify("1.0.0"));
		});
	});

	describe("getSyncPaths", () => {
		let res: ServerResponse;
		beforeEach(() => {
			res = mock<ServerResponse>();
		});

		it("should return sync paths", () => {
			router.getSyncPaths(res, mock<RegExpMatchArray>());

			expect(res.end).toHaveBeenCalledWith(
				JSON.stringify([
					{
						path: "plugins",
						enabled: true,
						enforced: false,
						silent: false,
						restartRequired: true,
					},
					{
						path: "user\\mods",
						enabled: true,
						enforced: false,
						silent: false,
						restartRequired: false,
					},
					{
						path: "user\\cache",
						enabled: false,
						enforced: false,
						silent: false,
						restartRequired: false,
					},
				]),
			);
		});
	});

	describe("getHashes", () => {
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
				mods: {},
			},
		};

		let res: ServerResponse;
		beforeEach(() => {
			vol.reset();
			vol.fromNestedJSON(directoryStructure);

			res = mock<ServerResponse>();
		});

		it("should return hashes", () => {
			router.getHashes(res, mock<RegExpMatchArray>());

			expect(res.end).toHaveBeenCalledWith(
				JSON.stringify({
					plugins: {
						"plugins\\file1.dll": {crc: 1338358949},
						"plugins\\OtherMod\\other_mod.dll": {crc: 2471037616},
					},
					"user\\mods": {},
					"user\\cache": {},
				}),
			);
		});
	});

	describe("fetchModFile", () => {
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
				mods: {},
			},
		};

		let res: ServerResponse;
		beforeEach(() => {
			vol.reset();
			vol.fromNestedJSON(directoryStructure);
			httpFileUtil.sendFile.mockClear();

			res = mock<ServerResponse>();
		});

		it("should return mod file", () => {
			router.fetchModFile(res, [
				"/modsync/fetch/plugins/file1.dll",
				"plugins/file1.dll",
			]);

			expect(res.setHeader).toHaveBeenCalledWith("Content-Length", 12);
			expect(httpFileUtil.sendFile).toHaveBeenCalledWith(
				res,
				"plugins\\file1.dll",
			);
		});

		it("should reject on non-existent path", () => {
			expect(() => {
				router.fetchModFile(res, [
					"/modsync/fetch/plugins/banana.dll",
					"plugins/banana.dll",
				]);
			}).toThrowError(
				new HttpError(
					404,
					"Attempt to access non-existent path plugins/banana.dll",
				),
			);
		});
	});
});
