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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.ILSpy.AppEnv;
using ICSharpCode.ILSpy.Deobfuscation.Core;

namespace ICSharpCode.ILSpy.Deobfuscation.UI
{
	/// <summary>
	/// Single entry point the plugin's commands and options page share. Holds the one
	/// <see cref="DeobfuscationService"/> for the session.
	/// </summary>
	public sealed class DeobfuscationHost
	{
		static readonly Lazy<DeobfuscationHost> instance = new(() => new DeobfuscationHost());

		public static DeobfuscationHost Instance => instance.Value;

		readonly Lazy<DeobfuscationService> service;

		DeobfuscationHost()
		{
			service = new Lazy<DeobfuscationService>(() => {
				var settings = AppComposition.Current.GetExport<SettingsService>().GetSettings<DeobfuscationSettings>();
				// Proxy-aware, like the update check: a restricted network is the common case where
				// the download has to go through a corporate proxy.
				var client = new HttpClient(new HttpClientHandler { UseProxy = true, UseDefaultCredentials = true });
				return new DeobfuscationService(settings, De4DotPlatform.Current, client);
			});
		}

		public DeobfuscationService Service => service.Value;

		/// <summary>Whether de4dotEx publishes a build that runs on this machine.</summary>
		public static bool IsPlatformSupported => De4DotPlatform.IsSupported(De4DotPlatform.Current);

		public string DescribeStatus()
		{
			if (!Service.IsPlatformSupported)
				return "de4dotEx publishes no build for this platform, so deobfuscation is unavailable here.";
			if (Service.TryResolveExecutable(out var path))
				return "Using " + path;
			return "de4dotEx is not installed yet.";
		}

		public string DescribeInstall()
		{
			var installer = Service.CreateInstaller();
			if (installer == null)
				return string.Empty;
			string description = installer.Describe();
			if (Service.RequiresEmulation)
			{
				description += " This machine is ARM64, so the x64 build is used and runs under the "
					+ "operating system's x64 emulation.";
			}
			return description;
		}

		/// <returns>A message describing the outcome, for the options page.</returns>
		public async Task<string> InstallAsync(CancellationToken cancellationToken = default)
		{
			var installer = Service.CreateInstaller();
			if (installer == null)
				return DescribeStatus();
			try
			{
				string path = await installer.InstallAsync(null, cancellationToken).ConfigureAwait(true);
				return "Using " + path;
			}
			catch (Exception ex) when (ex is HttpRequestException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
			{
				return "Install failed: " + ex.Message;
			}
		}
	}
}
