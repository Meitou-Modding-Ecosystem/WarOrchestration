# Meitou mod template

Template for a [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) code mod repo in the Meitou-Modding-Ecosystem
org. GPL-3.0-or-later.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).
- Checking a built mod against the newest Meitou release (CI does it on every PR): [docs/refcheck.md](docs/refcheck.md).
- Publishing the mod to the Steam Workshop for the first time, then on every release: [docs/steam-workshop.md](docs/steam-workshop.md).
- Turning `.glb` models (Meshy, Hi3D, Blender) into buildable structures, no FCS needed: [docs/structures.md](docs/structures.md).

## Use this template

1. Create the repo from this template on GitHub (Use this template > Create a new repository), then clone it with
   `git clone --recurse-submodules <your repo URL>`.
2. Rename `ExampleMod` to your mod's name: the folders `ExampleMod` and `ExampleMod.Tests`, the files inside them named `ExampleMod*`, the
   `AssemblyName`/`RootNamespace` in both `.csproj` files, the namespaces, and the paths in `MeitouMod.slnx` (rename that file too).
3. In `meitou.json` set `id` (`<author-or-mod>.<name>`), `assemblies` and `entry`; keep the version only there.
   In `.github/workflows/release.yml` set the tag prefix, `mod-folder` and `slug` (`<mod-name>`, as in the tag `<mod-name>-v<version>`).
4. Open the `.slnx` in Visual Studio and press F5 ([docs/play.md](docs/play.md)).

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. After a pull that moves it:
  `git submodule update --init`.
- `<Mod>/`: the mod's project and `meitou.json`. `<Mod>.Tests/`: its tests (synthetic worlds, no game install needed).
  `<Mod>.Data/` (optional): a generator that writes `<Mod>.mod` from the installed game. `<Mod>/Structures/` (optional): `.glb` models
  imported as buildable structures on deploy ([docs/structures.md](docs/structures.md)).
- `meitou.lock.json`: the framework releases the mods build against
  ([framework lock](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/framework-lock.md)); the Framework update workflow moves it
  to new releases and merges when CI passes ([framework update](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/framework-update.md)).
- `Play/`: one-click build, deploy and run.
- `tools/`: `refcheck.sh` and `RefCheck/`, the engine-reference check ([docs/refcheck.md](docs/refcheck.md)); `StructureImport/`, the
  model import ([docs/structures.md](docs/structures.md)).
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`. A tag `<slug>-v<version>` on `release` publishes the mod's zip (`.github/workflows/release.yml`).

- New-repository branch protections and permanent beta/main/release reviews: [docs/branch-policy.md](docs/branch-policy.md). Templates copy source; GutenStructure applies settings through reconciliation.
