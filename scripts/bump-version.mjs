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

const pluginAssemblyInfoString = await Bun.file(
	"ModSync/Properties/AssemblyInfo.cs",
).text();
await Bun.write(
	"ModSync/Properties/AssemblyInfo.cs",
	pluginAssemblyInfoString
		.replace(
			`AssemblyVersion("${currentVersion}")`,
			`AssemblyVersion("${newVersion}")`,
		)
		.replace(
			`AssemblyFileVersion("${currentVersion}")`,
			`AssemblyFileVersion("${newVersion}")`,
		),
);

const patcherAssemblyInfoString = await Bun.file(
	"ModSync/ModSync.PrePatcher/Properties/AssemblyInfo.cs",
).text();
await Bun.write(
	"ModSync/ModSync.PrePatcher/Properties/AssemblyInfo.cs",
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

const PluginCsString = await Bun.file("ModSync/Plugin.cs").text();

await Bun.write(
	"ModSync/Plugin.cs",
	PluginCsString.replace(
		`BepInPlugin("corter.modsync", "Corter ModSync", "${currentVersion}")`,
		`BepInPlugin("corter.modsync", "Corter ModSync", "${newVersion}")`,
	),
);
