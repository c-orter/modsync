const { config, rm, mkdir, cp, pushd, popd, exec } = require("shelljs");
const packageJson = require("../package.json");

rm("-rf", "../dist");
mkdir("-p", "../dist/user/mods/corter-modsync/src");
mkdir("-p", "../dist/BepInEx/plugins");
mkdir("-p", "../dist/BepInEx/patchers");
cp("package.json", "../dist/user/mods/corter-modsync/");
cp("src/*", "../dist/user/mods/corter-modsync/src");

pushd("-q", "../");
exec("dotnet build --configuration Release");
popd("-q");

cp("../ModSync/bin/Release/Corter-ModSync.dll", "../dist/BepInEx/plugins/");
cp(
	"../ModSync.PrePatcher/bin/Release/Corter-ModSync-Patcher.dll",
	"../dist/BepInEx/patchers/",
);

pushd("-q", "../dist");
config.silent = true;
exec(`7z a -tzip Corter-ModSync-Server-v${packageJson.version}.zip user/`);
exec(`7z a -tzip Corter-ModSync-Client-v${packageJson.version}.zip BepInEx/`);
config.silent = false;
popd("-q");
