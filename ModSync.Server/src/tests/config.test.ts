import { expect, beforeEach, describe, it, vi } from "vitest";

import { Config, ConfigUtil } from "../config";
import { VFS } from "./utils/vfs";
import { JsonUtil } from "./utils/jsonUtil";
import { PreSptModLoader } from "./utils/preSptModLoader";
import type { VFS as IVFS } from "@spt/utils/VFS";
import type { JsonUtil as IJsonUtil } from "@spt/utils/JsonUtil";
import type { PreSptModLoader as IPreSptModLoader } from "@spt/loaders/PreSptModLoader";
import { fs, vol } from "memfs";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import { mock } from "vitest-mock-extended";

vi.mock("node:fs", async () => (await vi.importActual("memfs")).fs);

describe("Config", () => {
	let config: Config;
	beforeEach(() => {
		config = new Config(
			[
				{ path: "plugins", enabled: true, enforced: false, silent: false },
				{ path: "mods", enabled: false, enforced: false, silent: false },
			],
			["plugins/**/node_modules", "plugins/**/*.js"],
		);
	});

	it("should correctly identify excluded paths", () => {
		expect(config.isExcluded("plugins/test.dll")).toBe(false);
		expect(config.isExcluded("plugins/banana/node_modules")).toBe(true);
		expect(config.isExcluded("plugins/banana/test.js")).toBe(true);
		expect(config.isExcluded("plugins/banana/config.json")).toBe(false);
	});

	it("should correctly identify children of excluded paths", () => {
		expect(config.isParentExcluded("plugins/test.dll")).toBe(false);
		expect(config.isParentExcluded("plugins/banana/node_modules/lodash")).toBe(
			true,
		);
	});
});

describe("ConfigUtil", () => {
	beforeEach(() => {
		vol.reset();
	});

	it("should load config", () => {
		vol.fromNestedJSON({
			"config.jsonc": `{
				"syncPaths": [
					"plugins",
					{ "path": "mods", "enabled": false },
					{ "path": "doesnotexist", "enabled": true }
				],
				// Exclusions for commonly used SPT mods
				"commonModExclusions": [
					"plugins/**/node_modules"
				]
			}`,
			plugins: {},
			mods: {},
		});

		const config = new ConfigUtil(
			new VFS() as IVFS,
			new JsonUtil() as IJsonUtil,
			new PreSptModLoader() as IPreSptModLoader,
			mock<ILogger>(),
		).load();

		expect(config.syncPaths).toEqual([
			{
				enabled: true,
				enforced: false,
				path: "plugins",
				restartRequired: true,
				silent: false,
			},
			{
				enabled: false,
				enforced: false,
				path: "mods",
				restartRequired: true,
				silent: false,
			},
			{
				enabled: true,
				enforced: false,
				path: "doesnotexist",
				restartRequired: true,
				silent: false,
			},
		]);
		expect(config.commonModExclusions).toEqual(["plugins/**/node_modules"]);
	});

	it("should reject absolute paths", () => {
		vol.fromNestedJSON(
			{
				"config.jsonc": `{
					"syncPaths": [
						"/etc/shadow",
						"C:\\Windows\\System32\\cmd.exe",
					],
					// Exclusions for commonly used SPT mods
					"commonModExclusions": [
						"plugins/**/node_modules"
					]
				}`,
			},
			"/tmp/src",
		);

		const configUtil = new ConfigUtil(
			new VFS() as IVFS,
			new JsonUtil() as IJsonUtil,
			new PreSptModLoader() as IPreSptModLoader,
			mock<ILogger>(),
		);

		expect(() => {
			configUtil.load();
		}).toThrow();
	});

	it("should reject paths outside of SPT root", () => {
		vol.fromNestedJSON(
			{
				"config.jsonc": `{
					"syncPaths": [
						"../../etc/shadow"
					],
					// Exclusions for commonly used SPT mods
					"commonModExclusions": [
						"plugins/**/node_modules"
					]
				}`,
			},
			"/tmp/src",
		);

		const configUtil = new ConfigUtil(
			new VFS() as IVFS,
			new JsonUtil() as IJsonUtil,
			new PreSptModLoader() as IPreSptModLoader,
			mock<ILogger>(),
		);

		expect(() => {
			configUtil.load();
		}).toThrow();
	});

	it("should reject invalid JSON", () => {
		vol.fromNestedJSON(
			{
				"config.jsonc": `{
					"invalid": "invalid"
				}`,
			},
			"/tmp/src",
		);

		const configUtil = new ConfigUtil(
			new VFS() as IVFS,
			new JsonUtil() as IJsonUtil,
			new PreSptModLoader() as IPreSptModLoader,
			mock<ILogger>(),
		);

		expect(() => {
			configUtil.load();
		}).toThrow();
	});
});
