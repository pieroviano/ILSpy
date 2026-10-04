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

using System.Globalization;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	/// <param name="Sha256">Lower-case hex digest published with the release asset; the download is
	/// rejected unless it matches.</param>
	public sealed record De4DotAsset(string Name, string Sha256)
	{
		public string Url => string.Format(CultureInfo.InvariantCulture,
			"https://github.com/{0}/releases/download/{1}/{2}", De4DotRelease.OwnerRepo, De4DotRelease.Version, Name);
	}

	/// <summary>
	/// The de4dotEx release the plugin installs. Pinned rather than "latest" so results stay
	/// reproducible and the digests below remain meaningful; a newer release is reported to the user
	/// but never installed automatically.
	/// </summary>
	public static class De4DotRelease
	{
		public const string OwnerRepo = "GDATAAdvancedAnalytics/de4dotEx";
		public const string Version = "3.10.0";

		static readonly De4DotAsset Windows = new(
			"de4dotEx-" + Version + "-net10.0-win-x64.zip",
			"1edf0650f01161dff777c32f83b20aa2a89cee08e7da3e8fee9b57675351c1c5");

		static readonly De4DotAsset Linux = new(
			"de4dotEx-" + Version + "-net10.0-linux-x64.zip",
			"b1f8420c7aa8606a5437684ddec3dacc9ebc231e2cbcbf18ee742a0ed1f01f4e");

		/// <summary>The asset to download for <paramref name="host"/>, or <c>null</c> if there is none.</summary>
		public static De4DotAsset? ForHost(De4DotHost host) => host switch {
			De4DotHost.WindowsX64 or De4DotHost.WindowsArm64 => Windows,
			De4DotHost.LinuxX64 => Linux,
			_ => null,
		};

		public static string ExecutableName(De4DotHost host)
			=> host is De4DotHost.WindowsX64 or De4DotHost.WindowsArm64 ? "de4dot.exe" : "de4dot";
	}
}
