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

using System.Runtime.InteropServices;

using AwesomeAssertions;

using ICSharpCode.ILSpy.Deobfuscation.Core;

using NUnit.Framework;

namespace ICSharpCode.ILSpy.Deobfuscation.Tests
{
	/// <summary>
	/// de4dotEx publishes net10.0 builds for win-x64 and linux-x64 only. Windows on ARM is served by
	/// the x64 build through the OS emulator; everything else has no build at all and the plugin must
	/// offer nothing there.
	/// </summary>
	[TestFixture]
	public class De4DotPlatformTests
	{
		[TestCase(true, false, Architecture.X64, De4DotHost.WindowsX64)]
		[TestCase(true, false, Architecture.Arm64, De4DotHost.WindowsArm64)]
		[TestCase(false, true, Architecture.X64, De4DotHost.LinuxX64)]
		[TestCase(false, true, Architecture.Arm64, De4DotHost.Unsupported)]
		[TestCase(false, false, Architecture.Arm64, De4DotHost.Unsupported)]
		[TestCase(false, false, Architecture.X64, De4DotHost.Unsupported)]
		[TestCase(true, false, Architecture.X86, De4DotHost.Unsupported)]
		public void Classify_maps_each_host(bool isWindows, bool isLinux, Architecture architecture, De4DotHost expected)
		{
			De4DotPlatform.Classify(isWindows, isLinux, architecture).Should().Be(expected);
		}

		[Test]
		public void Only_windows_on_arm_needs_emulation()
		{
			De4DotPlatform.RequiresEmulation(De4DotHost.WindowsArm64).Should().BeTrue();
			De4DotPlatform.RequiresEmulation(De4DotHost.WindowsX64).Should().BeFalse();
			De4DotPlatform.RequiresEmulation(De4DotHost.LinuxX64).Should().BeFalse();
			De4DotPlatform.RequiresEmulation(De4DotHost.Unsupported).Should().BeFalse();
		}

		[Test]
		public void Supported_excludes_only_the_unsupported_host()
		{
			De4DotPlatform.IsSupported(De4DotHost.WindowsX64).Should().BeTrue();
			De4DotPlatform.IsSupported(De4DotHost.WindowsArm64).Should().BeTrue();
			De4DotPlatform.IsSupported(De4DotHost.LinuxX64).Should().BeTrue();
			De4DotPlatform.IsSupported(De4DotHost.Unsupported).Should().BeFalse();
		}

		/// <summary>macOS is the case this plugin exists to exclude, so pin it explicitly.</summary>
		[Test]
		public void MacOS_is_unsupported()
		{
			De4DotPlatform.Classify(isWindows: false, isLinux: false, Architecture.Arm64)
				.Should().Be(De4DotHost.Unsupported);
		}
	}
}
