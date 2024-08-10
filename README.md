# Corter's Mod Sync For SPT & Fika

## About The Project

This project allows clients to easily add/update/remove mods, keeping them in sync with the host when playing on a remote SPT/Fika server.

<table>
<tbody>
<tr>
<td>

![Updater Complete](https://github.com/user-attachments/assets/f66a09b5-e133-418f-abf9-466b619cd2c9)</td>
</tr>
<tr>
<td>Updater completed</td>
</tr>
</tbody>
</table>

<table>
<tbody>
<tr>
<td>

![Update Required](https://github.com/user-attachments/assets/03c3ed36-f6d3-4067-b1dc-48fe726ed489)</td>
<td>

![Update Progress](https://github.com/user-attachments/assets/c4ca8953-03be-4d3b-af96-6ee7f1ee3ce2)</td>
</tr>
<tr>
<td>Prompt to update</td>
<td>Update progress</td>
</tr>
</tbody>
</table>

## Getting Started

### Server Setup

1. Download the latest version of the server mod from the [GitHub Releases](https://github.com/c-orter/modsync/releases) page
2. Extract into your SPT folder like any other server mod
3. Start the server

> Look for the message `Mod: corter-modsync version: 0.0.0 by: Corter loaded` to ensure installation was successful

### Client Setup

1. Download the latest version of the client mod from the [GitHub Releases](https://github.com/c-orter/modsync/releases) page
2. Extract into your SPT/Fika folder like any other client mod
   > Ensure you have both `BepInEx/plugins/Corter-ModSync.dll` **AND** `BepInEx/patchers/Corter-ModSync-PrePatcher.dll`.
3. Start the client and enjoy!


## Configuration

### Server

> Modify `config.jsonc` in user/mods/corter-modsync

| Configuration | Description | Default |
| --- | --- | --- |
| `syncPath` | List of paths to sync (can be folders or files) | `["BepInEx/plugins", "BepInEx/config", "BepInEx/patchers", "user/mods"]` |
| `commonModExclusions` | List of files from common mods that should be excluded from syncing | See [config.json](src/config.jsonc) |

### Client

> Modify `corter.modsync.cfg` in BepInEx/config or use the config manager with F12

| Configuration | Description | Default |
| --- | --- | --- |
| `Delete Removed Files` | Should files that have been removed from the host be deleted from the client | `false` |
| `[Synced Paths]` | A config entry will be added for each syncPath, allowing the client to customize the syncing behavior | `true` |

## Ignoring Files & Folders

Sometimes you may not want clients to download certain files (for example if you as the host have client mods other users don't want).
Or maybe you as a client have modified configs for some of your client mods and don't want those changes overriden.

**Well fear not!**

For **files** that you don't want synced, create a new, empty file next to it with the same name followed by `.nosync`, for example:

> This can typically be done by right clicking, hovering 'New', and selecting 'Text Document', then deleting the entire name and entering `.nosync`
> NOTE: .nosync.txt is also supported because Windows is cringe and doesn't show file extensions by default

For example, if you didn't want to sync the DeClutterer mod, you would create a new file in `BepInEx/plugins` named `TYR_DeClutterer.dll.nosync`
```sh
$ ls BepInEx/plugins
...
TYR_DeClutterer.dll
TYR_DeClutterer.dll.nosync
```

This can be cumbersome for mods with lots of files though, so as a shortcut you can exclude entire **folders** by adding a `.nosync` file to the folder.

For example, if you didn't want to sync the Donuts mod (why would you want that?), you would create a new file in `BepInEx/plugins/dvize.Donuts` named `.nosync`
```sh
ls BepInEx/plugins/dvize.Donuts
...
.nosync
```

> This convention works on both the server and the client so prevent syncing to your heart's content.

## Technical Explanation

**NOTE**: Ironically, this mod cannot update itself and is automatically excluded by the server. If you need to update it, you'll have to do it manually, like any other client mod.

This project is essentially a glorified HTTP wrapper with a few additional server routes added to the SPT server. It attempts to use CRC hashes and
UTC modification timestamps to more accurately determine when a file is actually changed, but in reality I haven't tested it that extensively.

Currently the way client mods are synced is by serving any file in `BepInEx/plugins` and `BepInEx/config` from the server. Similarly, server mods are
served from `user/mods`.

## Roadmap

- [x] Initial release
- [x] Super nifty GUI for notifying user of mod changes and monitoring download progress
- [x] Ability to exclude files/folders from syncing from both client and server
- [x] Custom folder sync support (May be useful for cached bundles? or mods that add files places that aren't BepInEx/plugins, BepInEx/config, or user/mods)
- [x] Maybe cooler progress bar/custom UI (low priority)
- [ ] Allow user to upload their local mods folders to host. (Needs some form of authorization, could be cool though)
- [ ] Buttons to sync from the BepInEx config menu (F12)
- [x] Real tests?!? (low priority)
