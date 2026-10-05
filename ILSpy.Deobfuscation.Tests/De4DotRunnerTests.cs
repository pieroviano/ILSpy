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
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using ICSharpCode.ILSpy.Deobfuscation.Core;

using NUnit.Framework;

namespace ICSharpCode.ILSpy.Deobfuscation.Tests
{
	/// <summary>Records what would have been launched and returns a canned result.</summary>
	sealed class FakeProcessLauncher : IProcessLauncher
	{
		public string? FileName { get; private set; }
		public IReadOnlyList<string> Arguments { get; private set; } = Array.Empty<string>();
		public ProcessResult Result { get; set; } = new(0, string.Empty, string.Empty);
		public Func<ProcessResult>? OnRun { get; set; }

		public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
		{
			FileName = fileName;
			Arguments = arguments;
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(OnRun?.Invoke() ?? Result);
		}
	}

	[TestFixture]
	public class De4DotRunnerTests
	{
		const string Exe = @"C:\cache\de4dot.exe";

		static De4DotRunner Create(FakeProcessLauncher launcher) => new(Exe, launcher);

		[Test]
		public async Task Detect_launches_the_configured_executable_with_the_detect_arguments()
		{
			var launcher = new FakeProcessLauncher { Result = new(0, @"Detected Foo (a.dll)", "") };

			var result = await Create(launcher).DetectAsync(@"C:\in\a.dll", CancellationToken.None);

			launcher.FileName.Should().Be(Exe);
			launcher.Arguments.Should().Equal("-d", "-f", @"C:\in\a.dll");
			result.Name.Should().Be("Foo");
		}

		[Test]
		public async Task Detect_reports_no_obfuscator_when_de4dot_recognises_none()
		{
			var launcher = new FakeProcessLauncher { Result = new(0, "Skipping unknown obfuscator: a.dll", "") };

			(await Create(launcher).DetectAsync("a.dll", CancellationToken.None)).IsObfuscated.Should().BeFalse();
		}

		[Test]
		public async Task Deobfuscate_returns_the_requested_output_path_on_success()
		{
			var launcher = new FakeProcessLauncher { Result = new(0, "done", "") };

			var result = await Create(launcher).DeobfuscateAsync(@"C:\in\a.dll", @"C:\out\a.dll", new DeobfuscationOptions(), CancellationToken.None);

			result.Success.Should().BeTrue();
			result.OutputFile.Should().Be(@"C:\out\a.dll");
			launcher.Arguments.Should().ContainInOrder("-f", @"C:\in\a.dll", "-o", @"C:\out\a.dll");
		}

		[Test]
		public async Task A_non_zero_exit_is_a_failure_that_carries_what_de4dot_said()
		{
			var launcher = new FakeProcessLauncher { Result = new(1, "partial log", "Could not detect obfuscator") };

			var result = await Create(launcher).DeobfuscateAsync("a", "b", new DeobfuscationOptions(), CancellationToken.None);

			result.Success.Should().BeFalse();
			result.Error.Should().Contain("Could not detect obfuscator");
			result.Log.Should().Contain("partial log");
		}

		[Test]
		public void Cancellation_propagates_out_of_the_runner()
		{
			var launcher = new FakeProcessLauncher();
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			Assert.ThrowsAsync<OperationCanceledException>(
				() => Create(launcher).DetectAsync("a.dll", cts.Token));
		}

		/// <summary>
		/// de4dot waits on Console.ReadKey when it believes a human double-clicked it; redirecting
		/// stdin turns that into the exception it already handles, so it must always be redirected.
		/// </summary>
		[Test]
		public void The_child_never_inherits_a_console_that_could_block_it()
		{
			var startInfo = ProcessLauncher.CreateStartInfo(Exe, new[] { "-d", "-f", "a.dll" });

			startInfo.RedirectStandardInput.Should().BeTrue("otherwise de4dot can block on Console.ReadKey");
			startInfo.RedirectStandardOutput.Should().BeTrue();
			startInfo.RedirectStandardError.Should().BeTrue();
			startInfo.UseShellExecute.Should().BeFalse();
			startInfo.CreateNoWindow.Should().BeTrue();
			startInfo.FileName.Should().Be(Exe);
			startInfo.ArgumentList.Should().Equal("-d", "-f", "a.dll");
		}
	}
}
