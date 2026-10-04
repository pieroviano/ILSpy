# Deobfuscation support via de4dotEx

Plan for adding obfuscator detection and deobfuscation to ILSpy as a **plugin** that drives the
de4dotEx executable out-of-process, obtained from an official release on demand.

## 0. Blocking verification (do before writing code)

| # | Item | Why it blocks | How to check |
|---|---|---|---|
| B1 | **de4dotEx is GPLv3; ILSpy is MIT** (`LICENSE:1`). de4dot is never linked and never ships inside ILSpy; it is downloaded by the user, into a per-user cache, and run as a separate process. | Linking or bundling would make the distributed ILSpy a GPLv3 combined work. | Confirm no build target copies de4dot into `ILSpy/bin`, and that nothing references `de4dot.*` assemblies. |
| B2 | **Release source.** `tsautier/de4dotEx` (the repo originally named) publishes **no release assets** - only stale tags `3.2.0-3.2.2`. Its upstream `GDATAAdvancedAnalytics/de4dotEx` publishes the binaries this plan consumes. | The whole acquisition design depends on real assets existing. | `GET /repos/GDATAAdvancedAnalytics/de4dotEx/releases/latest` -> tag `3.10.0`, published 2026-09-17. |
| B3 | **Platform coverage.** 3.10.0 ships `net10.0` for **win-x64 and linux-x64 only** (plus `net48` and a `.deb`). There is **no macOS and no arm64 asset**, while ILSpy targets `win-x64;win-arm64;linux-x64;osx-arm64`. | On unsupported platforms the feature cannot auto-install. | Confirm the manual-path fallback (§3.2) is acceptable there, or that the feature simply reports unavailable. |
| B4 | de4dot's `-d` output format and exit codes are as assumed. | The detect parser depends on both. | Run `de4dot -d <sample>`: expect `Detected <Name> (<path>)` on stdout, exit `0`. Unknown obfuscators print `Skipping unknown obfuscator: <path>` only under `-v`. Source: `de4dot.cui/FilesDeobfuscator.cs`, `de4dot.cui/Program.cs` (`0` success, `1` on `UserException`/unhandled, else `ExitException.code`). |
| B5 | de4dot never blocks waiting for a key press. | `Program.Main` calls `Console.ReadKey` when `IsN00bUser()`; it throws `InvalidOperationException` (caught) only when stdin is redirected. | Always start the child with `RedirectStandardInput = true` and close stdin at once. Verify a run returns. |
| B6 | Dynamic string decryption works (or not) per OS. | de4dot resolves strings by invoking the target's own decrypter through `AssemblyServer*` helpers, which are .NET Framework executables. | Run `--strtyp delegate` on Windows and Linux; record the result. Expect Windows-only; if so the option is disabled elsewhere. |
| B7 | de4dot handles `net10.0` assemblies without corrupting them. | A bad rewrite yields an assembly ILSpy cannot read. | Deobfuscate an unobfuscated `net10.0` sample; confirm ILSpy still decompiles the output. |
| B8 | **Confirm the ilspycmd split (§1, last row).** | Two earlier decisions pull apart: "code in ILSpyX, UI + ilspycmd" vs "implement as a plugin". A UI plugin cannot be loaded by `ilspycmd`. | Confirm the reconciliation: runner in ILSpyX (MIT, no UI), UI in the plugin. Or drop `ilspycmd` support and put everything in the plugin. |

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
| Code split | Runner + options model in `ICSharpCode.ILSpyX/Deobfuscation/` (no UI types); all UI in the plugin. | Lets `ilspycmd` keep its flags while the UI ships as a plugin. **This reconciles two conflicting answers - confirm (B8).** |

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
- **Nothing is downloaded without consent.** The first use shows repo, version, asset, size and URL, and proceeds only on accept.
- Download with the proxy-aware `HttpClient` shape already used by `ILSpy/Updates/UpdateService.cs:66-69`; verify SHA-256 against the table before extracting; extract to a temp dir and move into place, so a failed install never leaves a half-extracted cache.
- Zip entries are extracted only under the destination root (guard against `..` traversal).
- **Manual override always wins:** a configured path in Options bypasses acquisition entirely. This is the only route on platforms with no asset (B3) and for offline or proxied environments.
- "Check for newer" queries `releases/latest` and reports the newer tag; it never auto-upgrades the pin.

### 3.3 Core runner - `ICSharpCode.ILSpyX/Deobfuscation/`

No UI types, so `ilspycmd` can use it (B8).

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

### 3.5 UI (in the plugin)

| Surface | Shape |
|---|---|
| Context menu "Deobfuscate" | `[ExportContextMenuEntry(Header = "Deobfuscate", Category = "Debug", Order = 430)]`, visible for a single `AssemblyTreeNode` with `IsLoadedAsValidAssembly`. Template: `ILSpy/Commands/SetTargetFrameworkContextMenuEntry.cs:36`. |
| Context menu "Detect obfuscator" | Runs detection, reports through `DockWorkspace.ShowTextInNewTab`. |

Run flow:

1. Resolve de4dot. If absent, offer the consented install (§3.2); stop if declined.
2. If dynamic string decryption is enabled, show the confirmation naming the risk; stop unless accepted.
3. Run in a frozen tab via `DockWorkspace.RunInNewTabAsync`, as `ILSpy/Commands/PdbGenerator.cs:116` does, so browsing cannot cancel it. The tab streams de4dot's stdout.
4. On success `assemblyList.OpenAssembly(tempPath)` (`ICSharpCode.ILSpyX/AssemblyList.cs:306`) and select the new node.

Detect-on-open: subscribe to the assembly list's collection-changed, filter to `!IsAutoLoaded`, debounce, run `DetectAsync` off the UI thread, and on a hit surface a non-modal offer. Never blocks the load path.

### 3.6 ilspycmd

Depends on B8. If kept, in `ICSharpCode.ILSpyCmd/IlspyCmdProgram.cs` alongside the symbol options (`:140-148`):

```csharp
[Option("--detect-obfuscator", "Detect the obfuscator used by the input assemblies and exit.", CommandOptionType.NoValue)]
public bool DetectObfuscatorFlag { get; }

[Option("--deobfuscate", "Deobfuscate the input with de4dot before decompiling.", CommandOptionType.NoValue)]
public bool DeobfuscateFlag { get; }

[Option("--de4dot-path <path>", "Path to the de4dot executable. Required; ilspycmd does not download it.", CommandOptionType.SingleValue)]
public string De4DotPath { get; }

[Option("--deobfuscate-strings", "Allow de4dot to decrypt strings by EXECUTING the target assembly's own decrypter. Only use on assemblies you trust.", CommandOptionType.SingleValue)]
public string DeobfuscateStrings { get; }
```

`--detect-obfuscator` is handled in `OnExecuteAsync` next to `ServeSymbolsPort` (`:304`); `--deobfuscate` substitutes the cleaned path in `PerformPerFileAction` (`:364`). The CLI never downloads: `--de4dot-path` (or `PATH`) is required.

## 4. Tests

TDD, red first (`AGENTS.md:61`). Tests needing the real tool skip via `Assert.Ignore` when it is not installed.

| Area | File | Cases |
|---|---|---|
| Argument building | `ILSpy.Tests/Deobfuscation/De4DotArgumentTests.cs` | Options map to documented flags; `-o` always present; `--strtyp delegate` only when dynamic decryption is allowed; forced type passes `-p`. No process started. |
| Output parsing | `ILSpy.Tests/Deobfuscation/De4DotOutputTests.cs` | `Detected SmartAssembly (x.dll)` parses; unknown/empty -> `IsObfuscated == false`; exit 1 -> failure carrying stderr. |
| Process handling | `ILSpy.Tests/Deobfuscation/De4DotRunnerTests.cs` | Against a stub executable: stdin closed (B5), cancellation kills the child, non-zero exit surfaces stderr. |
| Acquisition | `ILSpy.Tests/Deobfuscation/De4DotInstallerTests.cs` | Against a local zip and a fake HTTP handler (pattern: `ILSpy.Tests/Symbols/SymbolFixture.cs` `FakeHttpHandler`): digest mismatch rejects and leaves no cache; traversal entries rejected; a failed download leaves no partial install; configured path short-circuits. |
| Platform gating | same | Unsupported RID reports unavailable with the manual-path hint, never throws (B3). |
| Service round-trip | `ILSpy.Tests/Deobfuscation/DeobfuscationServiceTests.cs` | Skipped unless installed. Deobfuscate a `FixtureAssembly.Emit` assembly (`ILSpy.Tests/FixtureAssembly.cs:56`) and assert the output still loads as a `PEFile` (B7). |
| Settings | `ILSpy.Tests/Deobfuscation/DeobfuscationSettingsTests.cs` | XML round-trip; `DetectOnOpen` and `AllowDynamicStringDecryption` default false. |
| Plugin composition | `ILSpy.Tests/Deobfuscation/DeobfuscationPluginTests.cs` | Plugin exports resolve; entry visible only for a single valid assembly node. Templates: `ILSpy.Tests/Plugins/TestPluginCompositionTests.cs`, `ILSpy.Tests/AssemblyList/ReloadAssemblyContextMenuTests.cs:42`. |

## 5. Verification

```powershell
./restore.ps1
./build.ps1 -Configuration Debug --no-restore
dotnet test --solution ILSpy.sln --report-trx
```

Manual: with no de4dot installed, confirm the feature reports unavailable and offers the download; accept it and confirm the cache is populated and verified; run Deobfuscate on an obfuscated sample and confirm a second assembly appears, decompiles with readable names, and the original is unchanged; confirm `ILSpy/bin` contains no `de4dot*` file.

## 6. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **GPLv3 contamination** | ILSpy could not ship as MIT. | Out-of-process only; no `ProjectReference`; nothing copied into ILSpy's output; the binary lives in a per-user cache the user chose to populate. B1. |
| **Downloading and executing a binary** | Supply-chain exposure. | Pinned version, SHA-256 verified before extraction, HTTPS, explicit consent showing the URL, traversal-guarded extraction. |
| **Dynamic string decryption executes malware** | Host compromise while analysing a sample. | Off by default, per-run confirmation, never implied by another option, stated in the options page. |
| No macOS/arm64 asset | Feature cannot auto-install there. | B3; manual path override; clear "unavailable on this platform" message. |
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
server (`de4dot.mcp`); writing the cleaned assembly over the original; shipping de4dot with ILSpy.
