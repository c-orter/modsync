const { config, rm, mkdir, cp, pushd, popd, exec } = require("shelljs");
const packageJson = require("../package.json");

let configuration = "Release";
if (process.argv.includes("--debug"))
    configuration = "Debug";

rm("-rf", "../dist");
mkdir("-p", "../dist/user/mods/Corter-ModSync/src");
mkdir("-p", "../dist/BepInEx/plugins");
mkdir("-p", "../dist/BepInEx/patchers");
cp("package.json", "../dist/user/mods/Corter-ModSync/");
cp("src/*", "../dist/user/mods/Corter-ModSync/src");

pushd("-q", "../");
exec(`dotnet build -c ${configuration}`);
popd("-q");


pushd("-q", "../ModSync.Updater");
exec(`dotnet publish -c ${configuration} -r win-x64`);
popd("-q");

cp(`../ModSync/bin/${configuration}/Corter-ModSync.dll`, "../dist/BepInEx/plugins/");
cp(`../ModSync.Updater/bin/${configuration}/net8.0-windows/win-x64/publish/ModSync.Updater.exe`, "../dist/")

pushd("-q", "../dist");
config.silent = true;
exec(`7z a -tzip Corter-ModSync-Client-v${packageJson.version}.zip BepInEx/ ModSync.Updater.exe`);
exec(`7z a -tzip Corter-ModSync-Server-v${packageJson.version}.zip BepInEx/ ModSync.Updater.exe user/`);
config.silent = false;
popd("-q");
