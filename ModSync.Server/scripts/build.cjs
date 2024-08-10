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
exec(`MSBuild.exe -p:Configuration=${configuration} /p:IncludeNativeLibrariesForSelfExtract=true -p:PublishSingleFile=true /verbosity:minimal`);
popd("-q");

cp(`../ModSync/bin/${configuration}/Corter-ModSync.dll`, "../dist/BepInEx/plugins/");
cp(`../ModSync.Updater/bin/${configuration}/ModSync.Updater.exe`, "../dist/")

pushd("-q", "../dist");
config.silent = true;
exec(`7z a -tzip Corter-ModSync-Client-v${packageJson.version}.zip BepInEx/ ModSync.Updater.exe`);
exec(`7z a -tzip Corter-ModSync-Server-v${packageJson.version}.zip BepInEx/ ModSync.Updater.exe user/`);
config.silent = false;
popd("-q");
