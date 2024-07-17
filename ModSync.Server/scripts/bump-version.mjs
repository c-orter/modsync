import { parseArgs } from "util";
import inc from "semver/functions/inc";

const {
	values: { type },
	positionals,
} = parseArgs({
	args: Bun.argv,
	options: {
		type: {
			type: "string",
			default: "patch",
		},
	},
	allowPositionals: true,
});

const packageJson = await Bun.file("package.json").json();

const currentVersion =
	positionals.length > 2 ? positionals[2] : packageJson.version;

const newVersion = inc(currentVersion, type);

const packageJsonString = await Bun.file("package.json").text();
await Bun.write(
	"package.json",
	packageJsonString.replace(
		`"version": "${currentVersion}"`,
		`"version": "${newVersion}"`,
	),
);

const patcherAssemblyInfoString = await Bun.file(
	"../ModSync.PrePatcher/Properties/AssemblyInfo.cs",
).text();
await Bun.write(
	"../ModSync.PrePatcher/Properties/AssemblyInfo.cs",
	patcherAssemblyInfoString
		.replace(
			`AssemblyVersion("${currentVersion}")`,
			`AssemblyVersion("${newVersion}")`,
		)
		.replace(
			`AssemblyFileVersion("${currentVersion}")`,
			`AssemblyFileVersion("${newVersion}")`,
		),
);

const testsAssemblyInfoString = await Bun.file(
	"../ModSync.Tests/Properties/AssemblyInfo.cs",
).text();
await Bun.write(
	"../ModSync.Tests/Properties/AssemblyInfo.cs",
	testsAssemblyInfoString
		.replace(
			`AssemblyVersion("${currentVersion}")`,
			`AssemblyVersion("${newVersion}")`,
		)
		.replace(
			`AssemblyFileVersion("${currentVersion}")`,
			`AssemblyFileVersion("${newVersion}")`,
		),
);

const PluginCsString = await Bun.file("../ModSync/Plugin.cs").text();
await Bun.write(
	"../ModSync/Plugin.cs",
	PluginCsString.replace(
		`BepInPlugin("corter.modsync", "Corter ModSync", "${currentVersion}")`,
		`BepInPlugin("corter.modsync", "Corter ModSync", "${newVersion}")`,
	),
);
