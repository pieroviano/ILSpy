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

using AwesomeAssertions;

using ICSharpCode.ILSpy.Deobfuscation.Core;

using NUnit.Framework;

namespace ICSharpCode.ILSpy.Deobfuscation.Tests
{
	/// <summary>
	/// The release is pinned so a result stays reproducible and the download can be checked against a
	/// known digest. Only the net10.0 assets are consumed.
	/// </summary>
	[TestFixture]
	public class De4DotReleaseTests
	{
		[Test]
		public void Windows_hosts_share_the_x64_asset()
		{
			var x64 = De4DotRelease.ForHost(De4DotHost.WindowsX64);
			var arm = De4DotRelease.ForHost(De4DotHost.WindowsArm64);

			x64.Should().NotBeNull();
			arm.Should().NotBeNull();
			arm!.Name.Should().Be(x64!.Name, "win-arm64 runs the x64 build under emulation");
			x64.Name.Should().Be("de4dotEx-3.10.0-net10.0-win-x64.zip");
		}

		[Test]
		public void Linux_has_its_own_asset()
		{
			De4DotRelease.ForHost(De4DotHost.LinuxX64)!.Name
				.Should().Be("de4dotEx-3.10.0-net10.0-linux-x64.zip");
		}

		[Test]
		public void Unsupported_host_has_no_asset()
		{
			De4DotRelease.ForHost(De4DotHost.Unsupported).Should().BeNull();
		}

		[Test]
		public void Every_asset_carries_a_sha256_and_a_release_url()
		{
			foreach (var host in new[] { De4DotHost.WindowsX64, De4DotHost.WindowsArm64, De4DotHost.LinuxX64 })
			{
				var asset = De4DotRelease.ForHost(host)!;
				asset.Sha256.Should().MatchRegex("^[0-9a-f]{64}$", "the digest is compared against the download");
				asset.Url.Should().Be($"https://github.com/{De4DotRelease.OwnerRepo}/releases/download/{De4DotRelease.Version}/{asset.Name}");
			}
		}

		[Test]
		public void Executable_name_follows_the_host()
		{
			De4DotRelease.ExecutableName(De4DotHost.WindowsX64).Should().Be("de4dot.exe");
			De4DotRelease.ExecutableName(De4DotHost.WindowsArm64).Should().Be("de4dot.exe");
			De4DotRelease.ExecutableName(De4DotHost.LinuxX64).Should().Be("de4dot");
		}
	}
}
