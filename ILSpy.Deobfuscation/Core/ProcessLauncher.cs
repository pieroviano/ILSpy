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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

	/// <summary>Runs a child process to completion. Exists so the runner can be tested without one.</summary>
	public interface IProcessLauncher
	{
		Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
	}

	public sealed class ProcessLauncher : IProcessLauncher
	{
		public static ProcessStartInfo CreateStartInfo(string fileName, IReadOnlyList<string> arguments)
		{
			var startInfo = new ProcessStartInfo(fileName) {
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				// de4dot calls Console.ReadKey when it thinks a human double-clicked it. With stdin
				// redirected that throws InvalidOperationException, which de4dot already swallows, so
				// the process exits instead of waiting forever for a key that never comes.
				RedirectStandardInput = true,
			};
			foreach (var argument in arguments)
				startInfo.ArgumentList.Add(argument);
			return startInfo;
		}

		public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
		{
			using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments) };
			process.Start();
			// Close stdin at once: nothing is ever written to it, and leaving it open would let the
			// child wait on a read.
			process.StandardInput.Close();
			// Read both pipes while the process runs; a full pipe would otherwise deadlock it.
			var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
			var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
			try
			{
				await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
				return new ProcessResult(process.ExitCode,
					await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
			}
			catch (OperationCanceledException)
			{
				TryKill(process);
				throw;
			}
		}

		static void TryKill(Process process)
		{
			try
			{
				if (!process.HasExited)
					process.Kill(entireProcessTree: true);
			}
			catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
			{
				// The process ended between the check and the kill, or we may not signal it.
			}
		}
	}
}
