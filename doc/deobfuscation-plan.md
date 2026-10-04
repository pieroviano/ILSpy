# Deobfuscation support via de4dotEx

Plan for adding obfuscator detection and deobfuscation to ILSpy, backed by
[de4dotEx](https://github.com/tsautier/de4dotEx) as a git submodule.

## 0. Blocking verification (do before writing code)

| # | Item | Why it blocks | How to check |
|---|---|---|---|
| B1 | **de4dotEx is GPLv3; ILSpy is MIT** (`LICENSE:1`). ILSpy must never link de4dot in-process, and the ILSpy build must not copy `de4dot*` into ILSpy's output. | Linking or bundling makes the shipped ILSpy a GPLv3 combined work. Out-of-process invocation of a separate executable keeps the two separate programs. | Confirm with the maintainer that "locate, never bundle" is acceptable. Confirm no build target copies de4dot output into `ILSpy/bin`. |
| B2 | de4dot resolves correctly as a submodule and publishes a runnable `net10.0` exe. | The whole feature is a process call. | `git submodule add -b master https://github.com/tsautier/de4dotEx.git de4dotEx` then `dotnet publish -c Release -f net10.0 -o publish-net10.0 de4dot` inside it (mirrors its `build.ps1`). Confirm the produced file name per OS (`de4dot.exe` / `de4dot`). |
| B3 | de4dot's `-d` output format and exit codes are as assumed. | The detect parser depends on both. | Run `de4dot -d <sample>`; expect `Detected <Name> (<path>)` on stdout and exit code 0. Unknown obfuscators print `Skipping unknown obfuscator: <path>` only at `-v`. Source: `de4dot.cui/FilesDeobfuscator.cs`, `de4dot.cui/Program.cs` (`0` success, `1` on `UserException`/unhandled, `ExitException.code` otherwise). |
| B4 | de4dot never blocks waiting for a key press. | `Program.Main` calls `Console.ReadKey` when `IsN00bUser()`. It throws `InvalidOperationException` (caught) when stdin is redirected. | Always start the child with `RedirectStandardInput = true` and close stdin immediately. Verify a run returns without hanging. |
| B5 | Dynamic string decryption works (or does not) on the target OS. | de4dot resolves strings by invoking the target's own decrypter through `AssemblyServer*` helpers, which are .NET Framework executables. | Run `--strtyp delegate` on Windows and on Linux with a sample; record the outcome. Expect Windows-only. If so, the UI must disable the option elsewhere. |
| B6 | de4dot handles `net10.0` assemblies without corrupting them. | de4dot predates modern TFMs; a bad rewrite yields an assembly ILSpy cannot read. | Deobfuscate an unobfuscated `net10.0` sample and confirm ILSpy still decompiles the output identically. |

## 1. Decisions

| Decision | Choice | Consequence |
|---|---|---|
| Integration | **Out-of-process**: run the de4dot executable as a child process, temp file in / temp file out. | ILSpy stays MIT. No dnlib dependency. Costs one process spawn (~0.3-2 s) per detect or run; no progress granularity beyond parsing stdout. |
| Distribution | **Locate, never bundle.** Resolution order: configured path -> `de4dot`/`de4dot.exe` on `PATH` -> `de4dotEx/publish-net10.0/` in the repo (developer convenience). | Keeps GPL binaries out of ILSpy's output (B1). Precedent: `ILSpy/Util/GraphVizGraph.cs:64-66` already requires `dot` on `PATH` and surfaces absence as an error. |
| Submodule | `de4dotEx/`, branch `master`, **not** checked out by default and **not** in any ILSpy solution. | Mirrors `ILSpy-tests` (`.gitmodules`, `AGENTS.md:49-54`). Tests that need the tool `Assert.Ignore` when it is absent. |
| Where the code lives | `ICSharpCode.ILSpyX/Deobfuscation/`. | UI-agnostic and unit-testable; reused by `ilspycmd`. |
| Output | **Temp file, opened alongside** the original. | Original stays untouched and comparable. Temp dir removed at exit. |
| On-load behaviour | Detection runs **only for explicitly opened assemblies** (never auto-loaded references), in the background, off by default, and only **offers** to deobfuscate. | A `IFileLoader` would spawn a process for every file including hundreds of auto-loaded references. See §3.4. |
| String decryption | Opt-in per run, off by default, behind an explicit confirmation naming the risk. | It executes the target assembly's code. |
| Front-ends | ILSpy UI + `ilspycmd`. | Shared service in ILSpyX. |

**Rejected:** an `IFileLoader` that deobfuscates during load (`ICSharpCode.ILSpyX/FileLoaders/FileLoaderRegistry.cs:40-48`). It is the natural-looking hook and the shape `XamarinCompressedFileLoader` uses, but it runs for every loaded file, cannot prompt, and would make reference auto-loading spawn a process per assembly.

**Rejected:** in-process rewriting through `LoadedAssemblyExtensions.CreateCecilObjectModel` (`ICSharpCode.ILSpyX/LoadedAssemblyExtensions.cs:40`) + `AssemblyList.HotReplaceAssembly` (`ICSharpCode.ILSpyX/AssemblyList.cs:365`). No new dependency and no process, but it means reimplementing de4dot, which is the thing being integrated.

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

### 3.1 Submodule

`.gitmodules` gains:

```
[submodule "de4dotEx"]
	path = de4dotEx
	url = https://github.com/tsautier/de4dotEx.git
	branch = master
```

`.gitignore` gains `de4dotEx/publish-net10.0/`. The submodule is excluded from `ILSpy.sln` and every `.slnf`, so CI never builds GPL code.

`doc/third-party-notices.txt` gains a de4dotEx entry recording GPLv3 and that it is invoked as a separate program, never linked.

### 3.2 Core service — `ICSharpCode.ILSpyX/Deobfuscation/`

New files:

| File | Contents |
|---|---|
| `De4DotLocator.cs` | Resolves the executable (configured path -> `PATH` -> submodule publish dir). `TryLocate(out string path)`. |
| `DeobfuscationOptions.cs` | `record DeobfuscationOptions(bool Rename, string? KeepNames, bool PreserveTokens, StringDecryption Strings, string? ForcedObfuscatorType)`; `enum StringDecryption { None, Static, Dynamic }`. |
| `ObfuscatorDetectionResult.cs` | `record(string? Name, bool IsObfuscated, string RawOutput)`. |
| `De4DotRunner.cs` | Builds the argument list, runs the process, captures stdout/stderr, maps exit codes. The only place that knows the CLI. |
| `DeobfuscationService.cs` | `Task<ObfuscatorDetectionResult> DetectAsync(string file, CancellationToken)` and `Task<DeobfuscationResult> DeobfuscateAsync(string file, DeobfuscationOptions, CancellationToken)`; owns the session temp directory. |

Process rules in `De4DotRunner`, each tied to a blocking item:

- `RedirectStandardInput = true`, stdin closed immediately (B4).
- `RedirectStandardOutput/Error = true`, `UseShellExecute = false`, `CreateNoWindow = true`.
- Reads both streams concurrently, then `WaitForExitAsync(cancellationToken)`; on cancellation the child is killed with its tree.
- Output path is always passed explicitly via `-o`, so de4dot's default `-cleaned` naming is never relied on.

Detection parses the `Detected <Name> (<path>)` line (B3); absence of that line means not obfuscated.

### 3.3 Settings

`ILSpy/Options/DeobfuscationSettings.cs`, modelled on `ILSpy/Symbols/SymbolSettings.cs:31-83`:

```csharp
public sealed partial class DeobfuscationSettings : ObservableObject, ISettingsSection
{
    [ObservableProperty] string de4dotPath = string.Empty;      // empty = auto-locate
    [ObservableProperty] bool detectOnOpen;                      // off by default
    [ObservableProperty] bool renameSymbols = true;
    [ObservableProperty] string keepNames = string.Empty;
    [ObservableProperty] bool preserveTokens;
    [ObservableProperty] bool allowDynamicStringDecryption;      // off by default

    public XName SectionName => "DeobfuscationSettings";
    public void LoadFromXml(XElement e) { /* per-property, with the defaults above */ }
    public XElement SaveToXml() { /* SetAttributeValue per property */ }
}
```

Options page `ILSpy/Options/DeobfuscationSettingsViewModel.cs` with `[ExportOptionPage(Order = 36)]`, modelled on `ILSpy/Options/SymbolSettingsViewModel.cs:31-54`; view `DeobfuscationSettingsPanel.axaml` resolved by the `*ViewModel` -> `*View` convention. The page shows the resolved de4dot path (or a "not found" hint), a Browse button, and the toggles. Order 36 sits after Symbols (35), before ReadyToRun (40).

`ILSpy.Tests/Options/OptionsTabTests.cs:167-171` pins the page count and titles and must be updated (currently 4 pages; becomes 5).

### 3.4 UI

New resource strings (`ILSpy/Properties/Resources.resx` **and** `Resources.Designer.cs` — the designer file is hand-maintained here): `Deobfuscate`, `DeobfuscateWithOptions`, `DetectObfuscator`, `Deobfuscation`, `DeobfuscationNotFound`, `DeobfuscationDynamicStringWarning`.

| Surface | File | Shape |
|---|---|---|
| Context menu "Deobfuscate" | `ILSpy/Commands/DeobfuscateContextMenuEntry.cs` | `[ExportContextMenuEntry(Header = nameof(Resources.Deobfuscate), Category = "Debug", Order = 430)]`, visible for a single `AssemblyTreeNode` with `IsLoadedAsValidAssembly`. Template: `ILSpy/Commands/SetTargetFrameworkContextMenuEntry.cs:36-60`. |
| Context menu "Detect obfuscator" | same file | Runs detection, writes a short report through `DockWorkspace.ShowTextInNewTab`. |
| File menu | `ILSpy/Commands/DeobfuscateCommand.cs` | `[ExportMainMenuCommand(ParentMenuID = nameof(Resources._File), MenuCategory = nameof(Resources.Save), MenuOrder = 23)]`, next to Generate portable PDB (`ILSpy/Commands/FileCommands.cs:315`). |

Run flow (`ILSpy/Deobfuscation/DeobfuscationActions.cs`):

1. Resolve de4dot; if missing, show the "not found, set it in Options" message and stop.
2. If dynamic string decryption is enabled, show the confirmation naming the risk; stop unless accepted.
3. Run in a frozen tab via `DockWorkspace.RunInNewTabAsync`, as `ILSpy/Commands/PdbGenerator.cs:116` does, so browsing cannot cancel it. The tab shows de4dot's stdout.
4. On success, `assemblyList.OpenAssembly(tempPath)` (`ICSharpCode.ILSpyX/AssemblyList.cs:306`) and select the new node.

Detect-on-open (`DetectOnOpen` setting): subscribe to the assembly list's collection-changed, filter to `!IsAutoLoaded`, debounce, run `DetectAsync` off the UI thread, and on a hit surface a non-modal prompt offering Deobfuscate. Never blocks the load path.

### 3.5 ilspycmd

`ICSharpCode.ILSpyCmd/IlspyCmdProgram.cs`, alongside the existing symbol options (`:140-148`):

```csharp
[Option("--detect-obfuscator", "Detect the obfuscator used by the input assemblies and exit.", CommandOptionType.NoValue)]
public bool DetectObfuscatorFlag { get; }

[Option("--deobfuscate", "Deobfuscate the input with de4dot before decompiling. Writes the cleaned assembly to the output directory when one is given.", CommandOptionType.NoValue)]
public bool DeobfuscateFlag { get; }

[Option("--de4dot-path <path>", "Path to the de4dot executable. Defaults to 'de4dot' on PATH.", CommandOptionType.SingleValue)]
public string De4DotPath { get; }

[Option("--deobfuscate-strings", "Allow de4dot to decrypt strings by EXECUTING the target assembly's own decrypter. Only use on assemblies you trust.", CommandOptionType.SingleValue)]
public string DeobfuscateStrings { get; }
```

`--detect-obfuscator` is handled in `OnExecuteAsync` next to `ServeSymbolsPort` (`:304`). `--deobfuscate` is applied in `PerformPerFileAction` (`:364`) by substituting the cleaned path before the normal action runs.

Note `ilspycmd` has its **own** `FileLoaderRegistry` (`ICSharpCode.ILSpyCmd/InputFileLoader.cs:56`), which is irrelevant here only because this design does not add a loader.

## 4. Tests

TDD, red first, per `AGENTS.md:61`. Tests that need the real executable skip via `Assert.Ignore` when `De4DotLocator.TryLocate` fails, mirroring the `ILSpy-tests` convention.

| Area | File | Cases |
|---|---|---|
| Argument building | `ILSpy.Tests/Deobfuscation/De4DotArgumentTests.cs` | Options map to the documented flags; `-o` always present; `--strtyp delegate` only when dynamic decryption is allowed; forced type passes `-p`. No process started. |
| Output parsing | `ILSpy.Tests/Deobfuscation/De4DotOutputTests.cs` | `Detected SmartAssembly (x.dll)` -> name parsed; unknown/empty -> `IsObfuscated == false`; exit 1 -> failure carrying stderr. |
| Locator | `ILSpy.Tests/Deobfuscation/De4DotLocatorTests.cs` | Configured path wins; missing file falls through; absent tool reports not-found rather than throwing. |
| Process handling | `ILSpy.Tests/Deobfuscation/De4DotRunnerTests.cs` | Against a stub executable: stdin is closed (B4), cancellation kills the child, non-zero exit surfaces stderr. |
| Service round-trip | `ILSpy.Tests/Deobfuscation/DeobfuscationServiceTests.cs` | `[Ignore]` unless the tool exists. Deobfuscate a `FixtureAssembly.Emit` assembly (`ILSpy.Tests/FixtureAssembly.cs:56`) and assert the output still loads as a `PEFile` (B6). |
| Settings | `ILSpy.Tests/Deobfuscation/DeobfuscationSettingsTests.cs` | XML round-trip; defaults (`DetectOnOpen` and `AllowDynamicStringDecryption` false). |
| UI entry | `ILSpy.Tests/Deobfuscation/DeobfuscateContextMenuTests.cs` | Entry registered; visible only for a single valid assembly node; missing tool produces the message instead of an exception. Template: `ILSpy.Tests/AssemblyList/ReloadAssemblyContextMenuTests.cs:42-96`. |
| Options page | `ILSpy.Tests/Options/OptionsTabTests.cs:167-171` | Updated count/titles. |

## 5. Verification

```powershell
git submodule update --init de4dotEx
cd de4dotEx; dotnet publish -c Release -f net10.0 -o publish-net10.0 de4dot; cd ..
./restore.ps1
./build.ps1 -Configuration Debug --no-restore
dotnet test --solution ILSpy.sln --report-trx
```

Manual: open an obfuscated sample, run Deobfuscate, confirm a second assembly appears and decompiles with readable names; confirm the original is unchanged; confirm ILSpy still starts and the feature degrades to a clear message when `de4dotEx/` was never initialised.

## 6. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **GPLv3 contamination** | ILSpy could not ship as MIT. | Out-of-process only; never a `ProjectReference`; no build step copies de4dot into ILSpy output; notices updated. B1. |
| **Dynamic string decryption executes malware** | Host compromise while analysing a sample. | Off by default, per-run confirmation, never implied by any other option, documented in the options page text. |
| Dynamic decryption is Windows-only | Silent failure on Linux/macOS. | B5; disable the option on other platforms once confirmed. |
| de4dot corrupts `net10.0` output | Cleaned assembly will not decompile. | B6; the original is never overwritten, so the user can always fall back. |
| Duplicate assembly identity in the list | The cleaned copy and the original share an identity, which can confuse reference resolution. | Accepted: ILSpy already tolerates several versions of an assembly. The node is labelled so the two are distinguishable. |
| Renaming changes tokens | Bookmarks and navigation history that point into the original do not map onto the cleaned copy. | Inherent to renaming; the two are separate assemblies in the tree, so nothing silently moves. |
| Process spawn latency on open | A slow, surprising pause after opening a file. | Detection is off by default, background-only, and never runs for auto-loaded references. |
| Submodule absent | Feature unavailable in a fresh clone. | Expected; the tool is located at runtime and its absence is a clear message, not an error. |

## 7. Out of scope

Deobfuscating assemblies inside bundles/packages (`LoadedPackage` entries have no on-disk path,
`ICSharpCode.ILSpyX/LoadedPackage.cs:401-408`); batch/recursive deobfuscation (`-r`); de4dot's MCP
server (`de4dot.mcp`); writing the cleaned assembly back over the original.
