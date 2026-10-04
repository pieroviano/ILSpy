# Deobfuscation support via de4dotEx

Plan for adding obfuscator detection and deobfuscation to ILSpy as a **plugin** that drives the
de4dotEx executable out-of-process, obtained from an official release on demand.

## 0. Blocking verification (do before writing code)

| # | Item | Why it blocks | How to check |
|---|---|---|---|
| B1 | **de4dotEx is GPLv3; ILSpy is MIT** (`LICENSE:1`). de4dot is never linked and never ships inside ILSpy; it is downloaded by the user, into a per-user cache, and run as a separate process. | Linking or bundling would make the distributed ILSpy a GPLv3 combined work. | Confirm no build target copies de4dot into `ILSpy/bin`, and that nothing references `de4dot.*` assemblies. |
| B2 | **Release source.** `tsautier/de4dotEx` (the repo originally named) publishes **no release assets** - only stale tags `3.2.0-3.2.2`. Its upstream `GDATAAdvancedAnalytics/de4dotEx` publishes the binaries this plan consumes. | The whole acquisition design depends on real assets existing. | `GET /repos/GDATAAdvancedAnalytics/de4dotEx/releases/latest` -> tag `3.10.0`, published 2026-09-17. |
| B3 | **Platform coverage.** 3.10.0 ships `net10.0` for **win-x64 and linux-x64 only**. There is **no macOS and no arm64 build**, while ILSpy targets `win-x64;win-arm64;linux-x64;osx-arm64`. | Decides where the plugin ships at all (§1.1). | Compare the `net10.0` asset names against `ILSpy/ILSpy.csproj:8`. |
| B3a | **win-arm64 runs the win-x64 build under Windows' x64 emulation.** Unverified. | win-arm64 ships the plugin on this assumption. | On a real ARM64 Windows device, install and run `de4dot -d <sample>`; confirm it executes and detects. If it fails, the install surfaces the error and the user can still supply a native build manually. |
| B4 | de4dot's `-d` output format and exit codes are as assumed. | The detect parser depends on both. | Run `de4dot -d <sample>`: expect `Detected <Name> (<path>)` on stdout, exit `0`. Unknown obfuscators print `Skipping unknown obfuscator: <path>` only under `-v`. Source: `de4dot.cui/FilesDeobfuscator.cs`, `de4dot.cui/Program.cs` (`0` success, `1` on `UserException`/unhandled, else `ExitException.code`). |
| B5 | de4dot never blocks waiting for a key press. | `Program.Main` calls `Console.ReadKey` when `IsN00bUser()`; it throws `InvalidOperationException` (caught) only when stdin is redirected. | Always start the child with `RedirectStandardInput = true` and close stdin at once. Verify a run returns. |
| B6 | Dynamic string decryption works (or not) per OS. | de4dot resolves strings by invoking the target's own decrypter through `AssemblyServer*` helpers, which are .NET Framework executables. | Run `--strtyp delegate` on Windows and Linux; record the result. Expect Windows-only; if so the option is disabled elsewhere. |
| B7 | de4dot handles `net10.0` assemblies without corrupting them. | A bad rewrite yields an assembly ILSpy cannot read. | Deobfuscate an unobfuscated `net10.0` sample; confirm ILSpy still decompiles the output. |
| B8 | ~~Confirm the ilspycmd split.~~ **Resolved: everything lives in the plugin; `ilspycmd` gets no deobfuscation support.** | Keeping core strictly untouched won over CLI support. | Nothing to check: the only files outside `ILSpy.Deobfuscation/` are `publish.ps1`, `ILSpy.sln` and `ILSpy.Desktop.slnf`. |

## 1. Decisions

| Decision | Choice | Consequence |
|---|---|---|
| Integration | **Out-of-process.** Run the de4dot executable as a child process, temp file in / temp file out. | ILSpy stays MIT. No dnlib dependency. Costs one process spawn (~0.3-2 s) per operation; progress only as parsed stdout. Also the only practical option: de4dotEx ships executables, not NuGet packages. |
| Packaging | **In-repo plugin**, `ILSpy.Deobfuscation`, `AssemblyName` = `ILSpy.Deobfuscation.Plugin`, `OutputPath` into ILSpy's `bin`. | Discovered by the `*.Plugin.dll` scan (`ILSpy/AppEnv/AppComposition.cs:119`). Mirrors `ILSpy.ReadyToRun` (`ILSpy.ReadyToRun/ILSpy.ReadyToRun.csproj:5,32`). Core ILSpy is untouched: no new strings in `ILSpy/Properties/Resources.resx`, no change to the options-page assertion (see §3.4). |
| Acquisition | **Download on demand, with consent**, from `GDATAAdvancedAnalytics/de4dotEx` releases, verified by SHA-256, extracted to a per-user cache. | Nothing GPL ships with ILSpy (B1). Requires network on first use and ~38 MB download / ~90 MB extracted. |
| Version | **Pinned to 3.10.0**, with an explicit "check for newer" action. | Reproducible; an upstream change cannot silently alter results. The pinned digests are the integrity check. |
| Source repo | `GDATAAdvancedAnalytics/de4dotEx`. | The repo originally named has no release assets (B2). `tsautier/de4dotEx` is a fork of this same 3.10.0 code. Owner/repo are settings, so repointing later needs no code change. |
| Submodule | **None.** | Dropped from the previous revision of this plan. No `.gitmodules` entry, no GPL source in the tree, no build step. |
| Output | **Temp file, opened alongside** the original. | Original untouched and comparable. Temp directory removed at exit. |
| On-load behaviour | Detection runs **only for explicitly opened assemblies**, in the background, off by default, and only **offers** to deobfuscate. | See the rejection below. |
| String decryption | Opt-in per run, off by default, behind a confirmation naming the risk. | It executes the target assembly's code. |
| Code split | **Everything in `ILSpy.Deobfuscation/`.** | Core is strictly untouched: outside the plugin only `publish.ps1` and the two solution files change. `ilspycmd` gets no deobfuscation support, because it cannot load a UI plugin (B8). |
| Platform support | **Supported platforms only** (§1.1): the plugin is not published on macOS, and its commands register nothing at runtime where de4dot cannot run. | No shipped artifact offers a feature that cannot work. **Core stays strictly untouched**, which leaves one developer-only wrinkle (§3.5). |

### 1.1 Platform support matrix

| ILSpy RID | de4dot asset | Plugin published | Notes |
|---|---|---|---|
| `win-x64` | `de4dotEx-<v>-net10.0-win-x64.zip` | Yes | Native. |
| `linux-x64` | `de4dotEx-<v>-net10.0-linux-x64.zip` | Yes | Native. |
| `win-arm64` | win-x64 asset, under emulation | Yes | **Unverified (B3a).** The consent dialog states that an x64 build will be downloaded and run under Windows' x64 emulation. |
| `osx-arm64` | none | **No** | de4dotEx publishes no macOS build. Omitted from the macOS publish, so it is absent from `ILSpy.app`. |
| anything else | none | n/a | Runtime gate reports unsupported and registers nothing. |

Only the `net10.0` assets are used.

**Rejected: an `IFileLoader` that deobfuscates during load** (`ICSharpCode.ILSpyX/FileLoaders/FileLoaderRegistry.cs:40`). It is the natural-looking hook and the shape `XamarinCompressedFileLoader` uses, but it runs for every loaded file, cannot prompt, and would spawn a process per auto-loaded reference.

**Rejected: in-process rewriting** through `LoadedAssemblyExtensions.CreateCecilObjectModel` (`ICSharpCode.ILSpyX/LoadedAssemblyExtensions.cs:40`) + `AssemblyList.HotReplaceAssembly` (`ICSharpCode.ILSpyX/AssemblyList.cs:365`). No process and no new dependency, but it means reimplementing de4dot.

## 2. de4dot CLI contract

Source: `de4dot.cui/CommandLineParser.cs`.

| Option | Meaning | Used for |
|---|---|---|
| `-f <file>` | input (also the default for bare args) | both |
| `-o <file>` | output | deobfuscate |
| `-d` | detect and exit | detect |
| `-p <type>` | force obfuscator type | advanced |
| `--dont-rename` | no symbol renaming | setting |
| `--keep-names <ntpefmagd>` | keep selected symbol kinds | setting |
| `--preserve-tokens` | preserve tokens/heaps | setting |
| `--strtyp <type>` / `--strtok <tok>` | string decrypter (`delegate` = dynamic) | opt-in |
| `-v` / `-vv` | verbosity | diagnostics |

Exit codes: `0` success, `1` failure.

## 3. Implementation

### 3.1 Plugin project

`ILSpy.Deobfuscation/ILSpy.Deobfuscation.csproj`, copying `ILSpy.ReadyToRun`:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <AssemblyName>ILSpy.Deobfuscation.Plugin</AssemblyName>
  <OutputPath>..\ILSpy\bin\$(Configuration)\</OutputPath>
  <Nullable>enable</Nullable>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="..\ILSpy\ILSpy.csproj" />
  <ProjectReference Include="..\ICSharpCode.ILSpyX\ICSharpCode.ILSpyX.csproj" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Avalonia" />
  <PackageReference Include="CommunityToolkit.Mvvm" />
</ItemGroup>
```

Added to `ILSpy.sln` and `ILSpy.Desktop.slnf` next to `ILSpy.ReadyToRun`. Plugin strings are literals or plugin-local resources; **core `Resources.resx` / `Resources.Designer.cs` are not touched.**

The project itself is a plain `net10.0` library and builds on every host, so CI stays uniform. Platform
support is expressed at **publish** time, in `publish.ps1`, which already lists each plugin per
platform:

| Branch | Change |
|---|---|
| `windows` | Publish the plugin alongside `ILSpy.ReadyToRun` for `win-x64` (framework-dependent and self-contained) and for `win-arm64`. |
| `linux` | Publish it alongside `ILSpy.ReadyToRun` for `linux-x64`. |
| `macos` | **No plugin publish.** `BuildMacAppBundle` snapshots the publish directory into `ILSpy.app/Contents/MacOS` (`ILSpy/ILSpy.csproj:165-168`), so omitting it here keeps it out of the bundle entirely. |

A developer build (`build.ps1`) still drops the DLL into `ILSpy/bin` on every OS, because `OutputPath`
is unconditional - which is exactly what the runtime gate in §3.5 covers.

### 3.2 Acquisition - `ILSpy.Deobfuscation/Acquisition/`

| File | Contents |
|---|---|
| `De4DotRelease.cs` | The pinned release as constants: version `3.10.0`, and per-RID asset name + SHA-256. |
| `De4DotInstaller.cs` | `IsInstalled`, `Describe()` (what would be downloaded), `InstallAsync(IProgress<double>, CancellationToken)`, `CheckForNewerAsync()`. |
| `De4DotCache.cs` | Resolves the cache root and the extracted executable path. |

Pinned assets (`https://github.com/GDATAAdvancedAnalytics/de4dotEx/releases/download/3.10.0/<name>`):

| RID | Asset | SHA-256 |
|---|---|---|
| `win-x64` | `de4dotEx-3.10.0-net10.0-win-x64.zip` | `1edf0650f01161dff777c32f83b20aa2a89cee08e7da3e8fee9b57675351c1c5` |
| `linux-x64` | `de4dotEx-3.10.0-net10.0-linux-x64.zip` | `b1f8420c7aa8606a5437684ddec3dacc9ebc231e2cbcbf18ee742a0ed1f01f4e` |

Rules:

- Cache root `%LOCALAPPDATA%/ILSpy/de4dot/<version>/`, matching the symbol cache convention (`ICSharpCode.ILSpyX/Symbols/SymbolPath.cs:49`: `GetFolderPath(LocalApplicationData, DoNotVerify)`).
- **Nothing is downloaded without consent.** The first use shows repo, version, asset, size and URL, and proceeds only on accept. On `win-arm64` it additionally states that the x64 build will be downloaded and run under Windows' x64 emulation (B3a).
- Asset selection: `win-x64` and `win-arm64` both take the win-x64 asset; `linux-x64` takes the linux-x64 asset. Anything else never reaches this code (§3.5).
- Download with the proxy-aware `HttpClient` shape already used by `ILSpy/Updates/UpdateService.cs:66-69`; verify SHA-256 against the table before extracting; extract to a temp dir and move into place, so a failed install never leaves a half-extracted cache.
- Zip entries are extracted only under the destination root (guard against `..` traversal).
- **Manual override always wins:** a configured path in Options bypasses acquisition entirely. This is the only route on platforms with no asset (B3) and for offline or proxied environments.
- "Check for newer" queries `releases/latest` and reports the newer tag; it never auto-upgrades the pin.

### 3.3 Runner - `ILSpy.Deobfuscation/Core/`

No UI types, so it stays unit-testable on its own.

| File | Contents |
|---|---|
| `DeobfuscationOptions.cs` | `record DeobfuscationOptions(bool Rename, string? KeepNames, bool PreserveTokens, StringDecryption Strings, string? ForcedObfuscatorType)`; `enum StringDecryption { None, Static, Dynamic }`. |
| `ObfuscatorDetectionResult.cs` | `record(string? Name, bool IsObfuscated, string RawOutput)`. |
| `De4DotRunner.cs` | Builds arguments, runs the process, captures stdout/stderr, maps exit codes. The only place that knows the CLI. |
| `DeobfuscationService.cs` | `DetectAsync(string file, CancellationToken)`, `DeobfuscateAsync(string file, DeobfuscationOptions, CancellationToken)`; owns the session temp directory. |

Process rules, each tied to a blocking item:

- `RedirectStandardInput = true`, stdin closed immediately (B5).
- `RedirectStandardOutput/Error = true`, `UseShellExecute = false`, `CreateNoWindow = true`.
- Read both streams concurrently, then `WaitForExitAsync(cancellationToken)`; on cancellation kill the process tree.
- Always pass `-o` explicitly, so de4dot's default `-cleaned` naming is never relied on.
- Detection parses the `Detected <Name> (<path>)` line (B4); its absence means not obfuscated.

### 3.4 Settings and options page (in the plugin)

`DeobfuscationSettings : ObservableObject, ISettingsSection`, modelled on `ILSpy/Symbols/SymbolSettings.cs:31`:

```csharp
[ObservableProperty] string de4dotPath = string.Empty;   // empty = use the managed cache
[ObservableProperty] string releaseOwnerRepo = "GDATAAdvancedAnalytics/de4dotEx";
[ObservableProperty] bool detectOnOpen;                   // off by default
[ObservableProperty] bool renameSymbols = true;
[ObservableProperty] string keepNames = string.Empty;
[ObservableProperty] bool preserveTokens;
[ObservableProperty] bool allowDynamicStringDecryption;   // off by default
public XName SectionName => "DeobfuscationSettings";
```

Sections register implicitly through `SettingsServiceBase.GetSettings<T>()`, so no manifest entry is needed.

Options page `[ExportOptionPage(Order = 45)]`, modelled on `ILSpy/Options/SymbolSettingsViewModel.cs:31`; the view resolves by the `*ViewModel` -> `*View` convention. It shows install state, the resolved path, Install / Check for newer / Browse, and the toggles.

Because the page ships in a plugin, it does **not** appear in the headless test container - `ILSpy.Tests/Options/OptionsTabTests.cs:167-171` asserts exactly 4 pages and already excludes `ILSpy.ReadyToRun`'s page (Order 40). **Confirm this holds when implementing**; if plugin pages do load there, update that assertion.

### 3.5 UI and the runtime platform gate (in the plugin)

`De4DotPlatform.cs` is the single source of truth:

```csharp
public static bool IsSupported =>
    (OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture is Architecture.X64 or Architecture.Arm64)
    || (OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture is Architecture.X64);

/// <summary>win-arm64 runs the x64 build under emulation; the consent dialog says so.</summary>
public static bool RequiresEmulation =>
    OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture is Architecture.Arm64;
```

| Surface | Shape |
|---|---|
| Context menu "Deobfuscate" | `[ExportContextMenuEntry(Header = "Deobfuscate", Category = "Debug", Order = 430)]`, `IsVisible` requires `De4DotPlatform.IsSupported` plus a single `AssemblyTreeNode` with `IsLoadedAsValidAssembly`. Template: `ILSpy/Commands/SetTargetFrameworkContextMenuEntry.cs:36`. An invisible entry is omitted from the built menu, so nothing shows. |
| Context menu "Detect obfuscator" | Same gate; runs detection and reports through `DockWorkspace.ShowTextInNewTab`. |
| Options page | Exported normally. It cannot be gated, and is not: see below. |

**The options page is the one thing the gate cannot hide, and core stays untouched anyway.**
`OptionsPageModel` materialises **every** exported `IOptionPage` with no filtering hook
(`ILSpy/Options/OptionsPageModel.cs:47-51`), and `IOptionsMetadata` is static (`Order` only), so a
plugin cannot express "only on these platforms" without changing core.

This costs nothing in any shipped artifact: the macOS bundle contains no plugin (§3.1), so there is no
page to hide. The only case is a **developer build on macOS**, where `build.ps1` copies the DLL into
`ILSpy/bin` and an inert Deobfuscation page appears in Options while the context-menu entries correctly
stay hidden. That is accepted.

Rejected alternatives: adding an `IConditionalOptionPage` filter to `OptionsPageModel` (three lines, but
it is a core change); and dropping the options page in favour of a plugin-owned dialog (gates perfectly,
but moves settings out of the conventional location for every user on every supported platform, to fix a
developer-only cosmetic issue).

Run flow:

1. Resolve de4dot. If absent, offer the consented install (§3.2); stop if declined.
2. If dynamic string decryption is enabled, show the confirmation naming the risk; stop unless accepted.
3. Run in a frozen tab via `DockWorkspace.RunInNewTabAsync`, as `ILSpy/Commands/PdbGenerator.cs:116` does, so browsing cannot cancel it. The tab streams de4dot's stdout.
4. On success `assemblyList.OpenAssembly(tempPath)` (`ICSharpCode.ILSpyX/AssemblyList.cs:306`) and select the new node.

Detect-on-open: subscribe to the assembly list's collection-changed, filter to `!IsAutoLoaded`, debounce, run `DetectAsync` off the UI thread, and on a hit surface a non-modal offer. Never blocks the load path.

## 4. Tests

Tests live in **`ILSpy.Deobfuscation.Tests`**, a project of its own rather than in `ILSpy.Tests`. A
plain `ProjectReference` from `ILSpy.Tests` would copy `ILSpy.Deobfuscation.Plugin.dll` into its
output, where `AppComposition`'s `*.Plugin.dll` scan would load it and add an options page to every
headless test - exactly what `TestPlugin` is referenced with `ReferenceOutputAssembly="false"` to
avoid. Everything worth testing here is pure logic, so no Avalonia harness is needed.

| File | Covers |
|---|---|
| `De4DotPlatformTests.cs` | The host matrix, including that macOS is `Unsupported` and only win-arm64 needs emulation. |
| `De4DotReleaseTests.cs` | Both Windows hosts share the x64 asset; every asset has a 64-hex digest and a release URL; unsupported hosts have none. |
| `De4DotArgumentTests.cs` | Each option maps to the documented flag; `-f`/`-o` always explicit; `--strtyp delegate` only on request. |
| `De4DotOutputTests.cs` | `Detected <name> (<file>)` parsing, including names containing parentheses; no line means not obfuscated. |
| `De4DotRunnerTests.cs` | Arguments reach the launcher, exit codes map to success/failure, cancellation propagates, and the start info always redirects stdin (B5). |
| `De4DotInstallerTests.cs` | Digest mismatch, HTTP failure, zip traversal and a missing executable each install nothing; a present install is reused without downloading. |
| `DeobfuscationSettingsTests.cs` | Defaults (both risky options off), XML round-trip, and that dynamic decryption needs the setting as well as the request. |
| `DeobfuscationServiceTests.cs` | Executable resolution order, unsupported hosts resolving to nothing, unique output paths, and scratch cleanup. |
| `PluginCompositionTests.cs` | The assembly name ends in `.Plugin`, the exports carry the expected header/category/order, and the availability gate needs no MEF container. |

Not covered: a real de4dot run. That needs the GPLv3 tool on the machine, so it stays a manual check
(§5) rather than something CI would have to download.

## 5. Verification

```powershell
./restore.ps1
./build.ps1 -Configuration Debug --no-restore
dotnet test --solution ILSpy.sln --report-trx
```

Manual: with no de4dot installed, confirm the feature offers the download; accept it and confirm the cache is populated and verified; run Deobfuscate on an obfuscated sample and confirm a second assembly appears, decompiles with readable names, and the original is unchanged; confirm `ILSpy/bin` contains no `de4dot*` file.

Platform checks:

```powershell
./publish.ps1 -Configuration Release -Platform macos
# expect: no ILSpy.Deobfuscation.Plugin.dll in the publish dir or inside ILSpy.app
./publish.ps1 -Configuration Release -Platform windows   # expect: present for win-x64 and win-arm64
./publish.ps1 -Configuration Release -Platform linux     # expect: present for linux-x64
```

On a macOS dev build (where `build.ps1` still copies the DLL into `ILSpy/bin`), confirm no Deobfuscate
context-menu entries appear; the Options page is expected to be present there and inert (§3.5). On ARM64
Windows, confirm the consent dialog mentions emulation and that the installed tool actually runs (B3a).

## 6. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **GPLv3 contamination** | ILSpy could not ship as MIT. | Out-of-process only; no `ProjectReference`; nothing copied into ILSpy's output; the binary lives in a per-user cache the user chose to populate. B1. |
| **Downloading and executing a binary** | Supply-chain exposure. | Pinned version, SHA-256 verified before extraction, HTTPS, explicit consent showing the URL, traversal-guarded extraction. |
| **Dynamic string decryption executes malware** | Host compromise while analysing a sample. | Off by default, per-run confirmation, never implied by another option, stated in the options page. |
| No macOS asset | Feature cannot exist there. | B3; the plugin is not published on macOS and registers nothing if a dev build drops it there (§3.5). |
| win-arm64 emulation unverified | Install succeeds but de4dot fails to start on ARM Windows. | B3a; the consent dialog states the emulation up front, and a launch failure surfaces as a normal tool error with the manual-path override still available. |
| Dynamic decryption likely Windows-only | Silent failure elsewhere. | B6; disable the option on other platforms once confirmed. |
| de4dot corrupts `net10.0` output | Cleaned assembly will not decompile. | B7; the original is never overwritten. |
| Upstream release disappears or changes | Install breaks. | Pin + digests fail loudly; manual override remains. |
| Duplicate assembly identity in the list | Cleaned copy and original share an identity, which can confuse reference resolution. | Accepted: ILSpy already tolerates several versions. The node is labelled so they are distinguishable. |
| Renaming changes tokens | Bookmarks/history into the original do not map onto the cleaned copy. | Inherent to renaming; the two are separate assemblies, so nothing silently moves. |
| ~38 MB download, ~90 MB cache | Disk and first-use latency. | Shown before consent; cached per version; removable. |
| Plugin API drift | Plugin stops loading after an ILSpy change. | It lives in-repo and builds with the solution, so a break surfaces at build time. |

## 7. Out of scope

Deobfuscating assemblies inside bundles/packages (`LoadedPackage` entries have no on-disk path,
`ICSharpCode.ILSpyX/LoadedPackage.cs:401`); batch/recursive deobfuscation (`-r`); de4dot's MCP
server (`de4dot.mcp`); writing the cleaned assembly over the original; shipping de4dot with ILSpy;
`ilspycmd` support, which would require code outside the plugin (B8).
