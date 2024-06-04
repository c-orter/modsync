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

const serverZip = new AdmZip();
serverZip.addLocalFolder("dist/user/", "user");
await serverZip.writeZipPromise(`corter-modsync-server-${packageJson.version}.zip`)

const clientZip = new AdmZip();
clientZip.addLocalFolder("dist/BepInEx/", "BepInEx");
await clientZip.writeZipPromise(`corter-modsync-client-${packageJson.version}.zip`)
