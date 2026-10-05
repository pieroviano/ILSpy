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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.ILSpy.Deobfuscation.Acquisition;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	/// <summary>
	/// Ties the settings, the installed tool and the runner together, and owns the scratch directory
	/// the cleaned assemblies are written to.
	/// </summary>
	public sealed class DeobfuscationService : IDisposable
	{
		readonly DeobfuscationSettings settings;
		readonly De4DotHost host;
		readonly HttpClient httpClient;
		readonly De4DotCache cache;
		string? sessionDirectory;
		int outputCounter;

		public DeobfuscationService(DeobfuscationSettings settings, De4DotHost host, HttpClient httpClient, De4DotCache? cache = null)
		{
			this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
			this.host = host;
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			this.cache = cache ?? new De4DotCache();
		}

		/// <summary>The live settings section; panels bind to it, so it is never copied.</summary>
		public DeobfuscationSettings Settings => settings;

		public bool IsPlatformSupported => De4DotPlatform.IsSupported(host);

		/// <summary>Whether the build this host would download runs under emulation.</summary>
		public bool RequiresEmulation => De4DotPlatform.RequiresEmulation(host);

		/// <summary>
		/// An installer for this host, or <c>null</c> when de4dotEx publishes no build for it.
		/// </summary>
		public De4DotInstaller? CreateInstaller()
		{
			var asset = De4DotRelease.ForHost(host);
			return asset == null
				? null
				: new De4DotInstaller(cache, asset, De4DotRelease.ExecutableName(host), httpClient);
		}

		/// <summary>
		/// Finds the executable to run: an explicitly configured path wins, so an offline or
		/// restricted machine can supply its own build; otherwise the managed cache is used.
		/// </summary>
		public bool TryResolveExecutable(out string executablePath)
		{
			executablePath = string.Empty;
			if (!IsPlatformSupported)
				return false;
			string configured = settings.De4DotPath;
			if (!string.IsNullOrWhiteSpace(configured))
			{
				configured = configured.Trim();
				if (!File.Exists(configured))
					return false;
				executablePath = configured;
				return true;
			}
			return CreateInstaller() is { } installer && installer.TryGetInstalled(out executablePath);
		}

		public De4DotRunner? TryCreateRunner()
			=> TryResolveExecutable(out var path) ? new De4DotRunner(path) : null;

		public Task<ObfuscatorDetectionResult>? DetectAsync(string inputFile, CancellationToken cancellationToken)
			=> TryCreateRunner()?.DetectAsync(inputFile, cancellationToken);

		/// <summary>
		/// A path for the cleaned copy of <paramref name="inputFile"/>. The file name is kept so the
		/// assembly reads naturally in the tree, and each run gets its own directory so deobfuscating
		/// the same assembly twice does not overwrite the earlier result.
		/// </summary>
		public string CreateOutputPath(string inputFile)
		{
			sessionDirectory ??= Path.Combine(Path.GetTempPath(), "ILSpy", "Deobfuscated", Guid.NewGuid().ToString("N"));
			string directory = Path.Combine(sessionDirectory, Interlocked.Increment(ref outputCounter).ToString());
			Directory.CreateDirectory(directory);
			return Path.Combine(directory, Path.GetFileName(inputFile));
		}

		/// <summary>Removes the cleaned copies written during this session.</summary>
		public void Dispose()
		{
			if (sessionDirectory == null)
				return;
			try
			{
				if (Directory.Exists(sessionDirectory))
					Directory.Delete(sessionDirectory, recursive: true);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				// A tab may still hold one of the files open; it is scratch space either way.
			}
			sessionDirectory = null;
		}
	}
}
