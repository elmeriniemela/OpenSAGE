# Arch Linux setup

See the [developer guide](developer-guide.md) for general build and game installation instructions. Run the build and launch commands below from the repository root.

## Dependencies

Install the native libraries:

```shell
sudo pacman -Syu --needed sdl2-compat openal
```

You also need working graphics drivers for the renderer you use.

OpenSAGE requires a .NET 8 SDK version 8.0.400 or later. Arch's `dotnet-sdk-8.0` package may provide an older SDK feature band even when fully updated (for example, SDK 8.0.131). The AUR package [dotnet-sdk-8.0-bin](https://aur.archlinux.org/packages/dotnet-sdk-8.0-bin) packages Microsoft's binaries with a newer SDK feature band. Install it with your AUR helper, for example:

```shell
yay -S dotnet-sdk-8.0-bin
# Alternatively:
paru -S dotnet-sdk-8.0-bin
```

This package replaces the conflicting `dotnet-sdk-8.0` package and installs into the system .NET location; no shell path changes are needed. Alternatively, follow Microsoft's [installation script documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script) for a user-local installation.

## Build

Verify the SDK selected by `global.json` and build the launcher:

```shell
dotnet --version # must report 8.0.400 or a later 8.0 SDK
dotnet build src/OpenSage.Launcher -c Release
```

NuGet dependencies download automatically. The repository includes its shader compiler.

## Vulkan: missing `libdl`

The Vulkan loader may fail with `DllNotFoundException: Unable to load shared library 'libdl'` because it requests `libdl.so`, while Arch provides `/usr/lib/libdl.so.2`. After building, create a symlink beside the launcher:

```shell
ln -s /usr/lib/libdl.so.2 src/OpenSage.Launcher/bin/Release/net8.0/libdl.so
```

This workaround needs no `sudo` and leaves system libraries unchanged. Create the link in the output directory you actually run (adjust `Release` for debug builds, or use your publish directory). Recreate it if a clean build removes that directory.

## Run

Copy the original Generals installation folder from your Windows machine. Use the folder containing `generals.exe` and the `.big` files, including its subfolders. OpenSAGE does not include those game assets.

For example, with the installation copied to `~/Games/Generals`:

```shell
dotnet run --project src/OpenSage.Launcher -c Release --no-build -- \
  --renderer Vulkan --gamepath "$HOME/Games/Generals" > /tmp/sage.log 2>&1
```

The command redirects standard output and errors to `/tmp/sage.log`.
