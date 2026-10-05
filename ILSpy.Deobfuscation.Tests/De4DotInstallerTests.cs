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
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using ICSharpCode.ILSpy.Deobfuscation.Acquisition;
using ICSharpCode.ILSpy.Deobfuscation.Core;

using NUnit.Framework;

namespace ICSharpCode.ILSpy.Deobfuscation.Tests
{
	/// <summary>
	/// The installer downloads a GPLv3 executable and runs it, so the download is pinned by digest,
	/// extracted defensively, and never left half-applied.
	/// </summary>
	[TestFixture]
	public class De4DotInstallerTests
	{
		static string NewDir()
		{
			var dir = Path.Combine(Path.GetTempPath(), "ILSpyDeobfTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			return dir;
		}

		static byte[] Zip(params (string Path, string Content)[] entries)
		{
			var buffer = new MemoryStream();
			using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
			{
				foreach (var (path, content) in entries)
				{
					var entry = archive.CreateEntry(path);
					using var stream = entry.Open();
					stream.Write(Encoding.UTF8.GetBytes(content));
				}
			}
			return buffer.ToArray();
		}

		static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

		sealed class StubHandler : HttpMessageHandler
		{
			readonly byte[] body;
			readonly HttpStatusCode status;
			public int Requests { get; private set; }

			public StubHandler(byte[] body, HttpStatusCode status = HttpStatusCode.OK)
			{
				this.body = body;
				this.status = status;
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Requests++;
				return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
			}
		}

		static De4DotInstaller Create(string root, byte[] body, string digest, HttpStatusCode status = HttpStatusCode.OK)
		{
			var asset = new De4DotAsset("de4dotEx-test.zip", digest);
			return new De4DotInstaller(new De4DotCache(root), asset, "de4dot.exe", new HttpClient(new StubHandler(body, status)));
		}

		[Test]
		public async Task A_verified_download_is_extracted_and_the_executable_located()
		{
			var root = NewDir();
			var zip = Zip(("de4dot.exe", "binary"), ("de4dot.runtimeconfig.json", "{}"));

			var installer = Create(root, zip, Sha256(zip));
			var path = await installer.InstallAsync(null, CancellationToken.None);

			File.Exists(path).Should().BeTrue();
			Path.GetFileName(path).Should().Be("de4dot.exe");
			installer.TryGetInstalled(out var found).Should().BeTrue();
			found.Should().Be(path);
		}

		[Test]
		public async Task A_digest_mismatch_is_rejected_and_nothing_is_installed()
		{
			var root = NewDir();
			var zip = Zip(("de4dot.exe", "binary"));

			var installer = Create(root, zip, Sha256(Encoding.UTF8.GetBytes("something else")));

			var act = async () => await installer.InstallAsync(null, CancellationToken.None);

			await act.Should().ThrowAsync<InvalidDataException>();
			installer.TryGetInstalled(out _).Should().BeFalse();
			Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
				.Where(File.Exists).Should().BeEmpty("a rejected download must not leave files behind");
		}

		[Test]
		public async Task A_failed_download_installs_nothing()
		{
			var root = NewDir();

			var installer = Create(root, Array.Empty<byte>(), Sha256(Array.Empty<byte>()), HttpStatusCode.NotFound);

			var act = async () => await installer.InstallAsync(null, CancellationToken.None);

			await act.Should().ThrowAsync<HttpRequestException>();
			installer.TryGetInstalled(out _).Should().BeFalse();
		}

		[Test]
		public async Task Entries_that_escape_the_destination_are_refused()
		{
			var root = NewDir();
			var zip = Zip(("../escaped.txt", "nope"), ("de4dot.exe", "binary"));

			var installer = Create(root, zip, Sha256(zip));

			var act = async () => await installer.InstallAsync(null, CancellationToken.None);

			await act.Should().ThrowAsync<InvalidDataException>();
			File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "escaped.txt")).Should().BeFalse();
		}

		[Test]
		public async Task An_archive_without_the_executable_is_refused()
		{
			var root = NewDir();
			var zip = Zip(("readme.txt", "no tool here"));

			var act = async () => await Create(root, zip, Sha256(zip)).InstallAsync(null, CancellationToken.None);

			await act.Should().ThrowAsync<InvalidDataException>();
		}

		[Test]
		public async Task An_existing_install_is_reused_without_downloading_again()
		{
			var root = NewDir();
			var zip = Zip(("de4dot.exe", "binary"));
			var handler = new StubHandler(zip);
			var installer = new De4DotInstaller(new De4DotCache(root), new De4DotAsset("a.zip", Sha256(zip)),
				"de4dot.exe", new HttpClient(handler));

			await installer.InstallAsync(null, CancellationToken.None);
			await installer.InstallAsync(null, CancellationToken.None);

			handler.Requests.Should().Be(1, "the cached install is reused");
		}
	}
}
