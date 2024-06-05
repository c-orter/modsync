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

const assemblyInfoString = await Bun.file(
	"ModSync/Properties/AssemblyInfo.cs",
).text();
await Bun.write(
	"ModSync/Properties/AssemblyInfo.cs",
	assemblyInfoString
		.replace(
			`AssemblyVersion("${currentVersion}")`,
			`AssemblyVersion("${newVersion}")`,
		)
		.replace(
			`AssemblyFileVersion("${currentVersion}")`,
			`AssemblyFileVersion("${newVersion}")`,
		),
);
