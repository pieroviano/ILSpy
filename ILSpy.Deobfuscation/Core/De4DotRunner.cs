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
using System.Threading;
using System.Threading.Tasks;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	/// <param name="OutputFile">Where the cleaned assembly was written; meaningful only on success.</param>
	/// <param name="Log">Everything de4dot printed, shown to the user verbatim.</param>
	public sealed record DeobfuscationResult(bool Success, string OutputFile, string Log, string? Error);

	/// <summary>
	/// Drives one de4dot executable. de4dot is GPLv3, so it is never linked: it is a separate program
	/// that this class starts, feeds a file, and reads the output of.
	/// </summary>
	public sealed class De4DotRunner
	{
		readonly string executablePath;
		readonly IProcessLauncher launcher;

		public De4DotRunner(string executablePath, IProcessLauncher? launcher = null)
		{
			this.executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
			this.launcher = launcher ?? new ProcessLauncher();
		}

		public async Task<ObfuscatorDetectionResult> DetectAsync(string inputFile, CancellationToken cancellationToken)
		{
			var result = await launcher.RunAsync(executablePath, De4DotArguments.Detect(inputFile), cancellationToken)
				.ConfigureAwait(false);
			return De4DotOutput.ParseDetection(result.StandardOutput);
		}

		public async Task<DeobfuscationResult> DeobfuscateAsync(string inputFile, string outputFile,
			DeobfuscationOptions options, CancellationToken cancellationToken)
		{
			var arguments = De4DotArguments.Deobfuscate(inputFile, outputFile, options);
			var result = await launcher.RunAsync(executablePath, arguments, cancellationToken).ConfigureAwait(false);
			bool success = result.ExitCode == 0;
			// de4dot reports failures on stderr but still exits 1 with a useful stdout tail, so keep
			// both: the log goes in the report tab, the error into the message.
			string? error = success
				? null
				: string.IsNullOrWhiteSpace(result.StandardError)
					? $"de4dot exited with code {result.ExitCode}."
					: result.StandardError.Trim();
			return new DeobfuscationResult(success, outputFile, result.StandardOutput, error);
		}
	}
}
