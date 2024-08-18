const inc = require("semver/functions/inc");
const valid = require("semver/functions/valid");
const path = require("node:path");
const { pushd, popd, sed } = require("shelljs");

const packageJson = require("../package.json");
const currentVersion = packageJson.version;

let level = "patch";
let newVersion = null;

if (process.argv.length > 2) {
	if (process.argv[2].startsWith("--")) {
		level = process.argv[2].slice(2);
	} else if (valid(currentVersion)) {
		newVersion = process.argv[2];
	}
}

newVersion = newVersion ?? inc(currentVersion, level);

sed(
	"-i",
	`"version": "${currentVersion}"`,
	`"version": "${newVersion}"`,
	"package.json",
);

pushd("-q", "../ModSync");
sed(
	"-i",
	`"${currentVersion}"`,
	`"${newVersion}"`,
	"Properties/AssemblyInfo.cs",
);
sed("-i", `"${currentVersion}"`, `"${newVersion}"`, "Plugin.cs");
popd("-q");

pushd("-q", "../ModSync.Updater");
sed(
	"-i",
	`<Version>${currentVersion}</Version>`,
	`<Version>${newVersion}</Version>`,
	"ModSync.Updater.csproj",
);
popd("-q");

pushd("-q", "../ModSync.Tests");
sed(
	"-i",
	`"${currentVersion}"`,
	`"${newVersion}"`,
	"Properties/AssemblyInfo.cs",
);
popd("-q");
