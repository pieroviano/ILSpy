// Copyright (c) 2026 Piero Viano
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.AppEnv;
using ICSharpCode.ILSpy.Deobfuscation.Core;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpyX;

namespace ICSharpCode.ILSpy.Deobfuscation.UI
{
	/// <summary>What the Deobfuscate and Detect commands actually do.</summary>
	public static class DeobfuscationActions
	{
		public static async Task DetectAsync(LoadedAssembly assembly, DockWorkspace dockWorkspace)
		{
			var host = DeobfuscationHost.Instance;
			if (!await EnsureToolAsync(host, dockWorkspace).ConfigureAwait(true))
				return;

			var output = new AvaloniaEditTextOutput { Title = "Detect obfuscator" };
			await dockWorkspace.RunInNewTabAsync("Detect obfuscator", async token => {
				var result = await host.Service.DetectAsync(assembly.FileName, token)!.ConfigureAwait(false);
				output.WriteLine(Path.GetFileName(assembly.FileName));
				output.WriteLine(result.IsObfuscated
					? "Detected: " + result.Name
					: "No known obfuscator was detected.");
				output.WriteLine();
				output.Write(result.RawOutput);
				return output;
			}).ConfigureAwait(true);
		}

		public static async Task RunAsync(LoadedAssembly assembly, DockWorkspace dockWorkspace)
		{
			var host = DeobfuscationHost.Instance;
			if (!await EnsureToolAsync(host, dockWorkspace).ConfigureAwait(true))
				return;

			var service = host.Service;
			// Dynamic string decryption runs code out of the assembly being analysed, so it is only
			// ever requested when the user has turned it on in Options for exactly that reason.
			var options = service.Settings.ToOptions(decryptStrings: service.Settings.AllowDynamicStringDecryption);
			string outputFile = service.CreateOutputPath(assembly.FileName);

			DeobfuscationResult? result = null;
			await dockWorkspace.RunInNewTabAsync("Deobfuscate " + assembly.ShortName, async token => {
				var runner = service.TryCreateRunner()!;
				result = await runner.DeobfuscateAsync(assembly.FileName, outputFile, options, token).ConfigureAwait(false);
				var output = new AvaloniaEditTextOutput { Title = "Deobfuscate" };
				output.Write(result.Log);
				output.WriteLine();
				output.WriteLine(result.Success
					? "Wrote " + result.OutputFile
					: "Deobfuscation failed: " + result.Error);
				return output;
			}).ConfigureAwait(true);

			if (result is { Success: true } && File.Exists(result.OutputFile))
			{
				// Opened alongside the original so the two can be compared; the original file on disk
				// is untouched either way.
				assembly.AssemblyList.OpenAssembly(result.OutputFile);
			}
		}

		/// <summary>
		/// Makes sure de4dot is available, offering the download when it is not.
		/// </summary>
		static async Task<bool> EnsureToolAsync(DeobfuscationHost host, DockWorkspace dockWorkspace)
		{
			if (host.Service.TryResolveExecutable(out _))
				return true;

			var output = new AvaloniaEditTextOutput { Title = "Deobfuscation" };
			output.WriteLine(host.DescribeStatus());
			output.WriteLine();
			string install = host.DescribeInstall();
			if (install.Length > 0)
			{
				output.WriteLine(install);
				output.WriteLine();
				output.WriteLine("Open Options -> Deobfuscation to download it.");
			}
			dockWorkspace.ShowTextInNewTab("Deobfuscation", output);
			return false;
		}
	}
}
