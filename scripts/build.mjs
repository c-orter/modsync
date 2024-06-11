import { $ } from "bun";

const packageJson = await Bun.file("package.json").json();

await $`rm -rf dist`.nothrow().quiet();

await $`mkdir -p dist`;
await $`mkdir -p dist/user/mods/corter-modsync/src`;
await $`mkdir -p dist/BepInEx/plugins`;
await $`mkdir -p dist/BepInEx/patchers`;
await $`cp package.json dist/user/mods/corter-modsync/`;
await $`cp src/* dist/user/mods/corter-modsync/src`;

await $`dotnet build --configuration Release`.cwd("ModSync/");
await $`cp ModSync/bin/Release/Corter-ModSync.dll dist/BepInEx/plugins/`;
await $`cp ModSync.PrePatcher/bin/Release/Corter-ModSync-Patcher.dll dist/BepInEx/patchers/`;

await $`7z a -tzip corter-modsync-server-${packageJson.version}.zip user/`
	.cwd("dist/")
	.quiet();

await $`7z a -tzip corter-modsync-client-${packageJson.version}.zip BepInEx/`
	.cwd("dist/")
	.quiet();
