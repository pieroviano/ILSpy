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
using System.Runtime.InteropServices;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	/// <summary>A host de4dotEx publishes a net10.0 build for, or <see cref="Unsupported"/>.</summary>
	public enum De4DotHost
	{
		Unsupported,
		WindowsX64,
		/// <summary>Served by the x64 build through the operating system's x64 emulation.</summary>
		WindowsArm64,
		LinuxX64,
	}

	/// <summary>
	/// Decides whether de4dotEx can run here at all. Everything the plugin exposes is gated on this:
	/// where there is no build, the commands register nothing rather than offering a download that
	/// cannot work.
	/// </summary>
	public static class De4DotPlatform
	{
		/// <summary>The host this process is running on.</summary>
		public static De4DotHost Current { get; } = Classify(
			OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), RuntimeInformation.OSArchitecture);

		/// <param name="architecture">The operating system's architecture, not the process's: an x86
		/// process on x64 Windows still runs the x64 build.</param>
		public static De4DotHost Classify(bool isWindows, bool isLinux, Architecture architecture)
		{
			if (isWindows)
			{
				return architecture switch {
					Architecture.X64 => De4DotHost.WindowsX64,
					Architecture.Arm64 => De4DotHost.WindowsArm64,
					_ => De4DotHost.Unsupported,
				};
			}
			if (isLinux && architecture == Architecture.X64)
				return De4DotHost.LinuxX64;
			return De4DotHost.Unsupported;
		}

		public static bool IsSupported(De4DotHost host) => host != De4DotHost.Unsupported;

		/// <summary>
		/// Whether the build used on <paramref name="host"/> is for a different architecture and runs
		/// under emulation. The consent shown before downloading says so.
		/// </summary>
		public static bool RequiresEmulation(De4DotHost host) => host == De4DotHost.WindowsArm64;
	}
}
