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
import type { IncomingMessage, ServerResponse } from "node:http";
import { HttpError } from "../utility";
import { HttpServerHelper } from "@spt/helpers/HttpServerHelper";

vi.mock("node:fs", async () => (await vi.importActual("memfs")).fs);

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
				restartRequired: true,
			},
		],
		["plugins/**/node_modules"],
	);
	const vfs = new VFS() as IVFS;
	const logger = mock<ILogger>();
	const syncUtil = new SyncUtil(vfs, config, logger);
	const httpFileUtil = mock<HttpFileUtil>();
	const httpServerHelper = mock<HttpServerHelper>();
	const modImporter = new PreSptModLoader() as IPreSptModLoader;
	const router = new Router(
		config,
		syncUtil,
		vfs,
		httpFileUtil,
		httpServerHelper,
		modImporter,
		logger,
	);

	describe("getServerVersion", () => {
		let req = mock<IncomingMessage>({
			headers: { "modsync-version": "0.8.0" },
		});
		let res = mock<ServerResponse>();

		beforeEach(() => {
			vol.reset();
			vol.fromNestedJSON({ "package.json": '{ "version": "1.0.0" }' });

			req = mock<IncomingMessage>({ headers: { "modsync-version": "0.8.0" } });
			res = mock<ServerResponse>();
		});

		it("should return server version", async () => {
			await router.getServerVersion(req, res, mock<RegExpMatchArray>());

			expect(res.end).toHaveBeenCalledWith(JSON.stringify("1.0.0"));
		});
	});

	describe("getSyncPaths", () => {
		let req = mock<IncomingMessage>({
			headers: { "modsync-version": "0.8.0" },
		});
		let res = mock<ServerResponse>();

		beforeEach(() => {
			req = mock<IncomingMessage>({ headers: { "modsync-version": "0.8.0" } });
			res = mock<ServerResponse>();
		});

		it("should return sync paths", async () => {
			await router.getSyncPaths(req, res, mock<RegExpMatchArray>());

			expect(res.end.mock.calls).toMatchSnapshot();
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

		let req = mock<IncomingMessage>({
			headers: { "modsync-version": "0.8.0" },
		});
		let res = mock<ServerResponse>();
		beforeEach(() => {
			vol.reset();
			vol.fromNestedJSON(directoryStructure);

			req = mock<IncomingMessage>({ headers: { "modsync-version": "0.8.0" } });
			res = mock<ServerResponse>();
		});

		it("should return hashes", async () => {
			await router.getHashes(req, res, mock<RegExpMatchArray>());

			expect(res.end.mock.calls).toMatchSnapshot();
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

		let req = mock<IncomingMessage>({
			headers: { "modsync-version": "0.8.0" },
		});
		let res = mock<ServerResponse>();

		beforeEach(() => {
			vol.reset();
			vol.fromNestedJSON(directoryStructure);
			httpFileUtil.sendFile.mockClear();

			req = mock<IncomingMessage>({ headers: { "modsync-version": "0.8.0" } });
			res = mock<ServerResponse>();
		});

		it("should return mod file", async () => {
			await router.fetchModFile(req, res, [
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
			expect(
				router.fetchModFile(req, res, [
					"/modsync/fetch/plugins/banana.dll",
					"plugins/banana.dll",
				]),
			).rejects.toThrowError(
				new HttpError(
					404,
					"Attempt to access non-existent path plugins/banana.dll",
				),
			);
		});
	});
});
