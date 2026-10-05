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
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.ILSpy.Deobfuscation.Core;

namespace ICSharpCode.ILSpy.Deobfuscation.Acquisition
{
	/// <summary>
	/// Downloads the pinned de4dotEx release into the user's cache and unpacks it.
	/// <para>
	/// This fetches an executable and the plugin then runs it, so every step is deliberate: the
	/// version is pinned, the archive is checked against the digest published with the release before
	/// anything is unpacked, entries that would escape the destination are refused, and the install
	/// only becomes visible once it is complete.
	/// </para>
	/// </summary>
	public sealed class De4DotInstaller
	{
		readonly De4DotCache cache;
		readonly De4DotAsset asset;
		readonly string executableName;
		readonly HttpClient httpClient;

		public De4DotInstaller(De4DotCache cache, De4DotAsset asset, string executableName, HttpClient httpClient)
		{
			this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
			this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
			this.executableName = executableName ?? throw new ArgumentNullException(nameof(executableName));
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		}

		/// <summary>What the user is asked to agree to before anything is fetched.</summary>
		public string Describe()
			=> $"de4dotEx {De4DotRelease.Version} ({asset.Name}) will be downloaded from {asset.Url} "
				+ $"and unpacked into {cache.Root}. de4dotEx is licensed under the GPLv3 and is run as a separate program.";

		public bool TryGetInstalled(out string executablePath)
		{
			executablePath = Path.Combine(cache.Root, executableName);
			return File.Exists(executablePath);
		}

		/// <returns>The path of the installed executable.</returns>
		/// <exception cref="InvalidDataException">The download did not match the pinned digest, tried to
		/// write outside the destination, or did not contain the executable.</exception>
		public async Task<string> InstallAsync(IProgress<double>? progress, CancellationToken cancellationToken)
		{
			if (TryGetInstalled(out var existing))
				return existing;

			byte[] archive = await DownloadAsync(progress, cancellationToken).ConfigureAwait(false);
			Verify(archive);

			string staging = cache.NewStagingDirectory();
			try
			{
				Extract(archive, staging);
				string staged = Path.Combine(staging, executableName);
				if (!File.Exists(staged))
					throw new InvalidDataException($"'{asset.Name}' does not contain {executableName}.");

				Directory.CreateDirectory(Path.GetDirectoryName(cache.Root.TrimEnd(Path.DirectorySeparatorChar))!);
				// An earlier attempt may have left the versioned directory behind without a usable
				// executable -- TryGetInstalled said there is none -- and Move refuses an existing
				// destination, so clear it first.
				TryDelete(cache.Root);
				// Move last, so an interrupted install never leaves a partial tool that looks usable.
				Directory.Move(staging, cache.Root);
			}
			finally
			{
				TryDelete(staging);
			}

			TryGetInstalled(out var installed);
			return installed;
		}

		async Task<byte[]> DownloadAsync(IProgress<double>? progress, CancellationToken cancellationToken)
		{
			using var response = await httpClient.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
				.ConfigureAwait(false);
			response.EnsureSuccessStatusCode();

			long? total = response.Content.Headers.ContentLength;
			using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			using var buffer = new MemoryStream(capacity: total is > 0 and < int.MaxValue ? (int)total.Value : 0);
			var chunk = new byte[81920];
			int read;
			while ((read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
			{
				buffer.Write(chunk, 0, read);
				if (total is > 0)
					progress?.Report((double)buffer.Length / total.Value);
			}
			return buffer.ToArray();
		}

		void Verify(byte[] archive)
		{
			string actual = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
			if (!string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException(
					$"'{asset.Name}' does not match the digest published for de4dotEx {De4DotRelease.Version}. "
					+ $"Expected {asset.Sha256}, got {actual}.");
			}
		}

		/// <summary>
		/// Unpacks every entry under <paramref name="destination"/>, refusing any whose resolved path
		/// would land outside it.
		/// </summary>
		static void Extract(byte[] archive, string destination)
		{
			Directory.CreateDirectory(destination);
			string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
			using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
			foreach (var entry in zip.Entries)
			{
				string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
				if (!target.StartsWith(root, StringComparison.Ordinal))
					throw new InvalidDataException($"'{entry.FullName}' would be written outside the destination directory.");
				if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
				{
					Directory.CreateDirectory(target);
					continue;
				}
				Directory.CreateDirectory(Path.GetDirectoryName(target)!);
				entry.ExtractToFile(target, overwrite: true);
			}
		}

		static void TryDelete(string directory)
		{
			try
			{
				if (Directory.Exists(directory))
					Directory.Delete(directory, recursive: true);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
			}
		}
	}
}
