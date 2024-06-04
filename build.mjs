import { $ } from "bun";
import AdmZip from "adm-zip"

const packageJson = await Bun.file("package.json").json();

await $`mkdir -p dist`;
await $`mkdir -p dist/user/mods/corter-modsync/src`;
await $`mkdir -p dist/BepInEx/plugins`;
await $`cp package.json dist/user/mods/corter-modsync/`;
await $`cp src/* dist/user/mods/corter-modsync/src`;

await $`dotnet build --configuration Release`.cwd("ModSync/")
await $`cp ModSync/bin/Release/Corter-ModSync.dll dist/BepInEx/plugins/`
// await $`zip -r ../corter-modsync-${packageJson.version}.zip *`.cwd("dist")

const zip = new AdmZip();

zip.addLocalFolder("dist/user/", "user");
zip.addLocalFolder("dist/BepInEx/", "BepInEx");

await zip.writeZipPromise(`corter-modsync-${packageJson.version}.zip`)