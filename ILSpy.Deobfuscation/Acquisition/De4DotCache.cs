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

using ICSharpCode.ILSpy.Deobfuscation.Core;

namespace ICSharpCode.ILSpy.Deobfuscation.Acquisition
{
	/// <summary>
	/// Where the downloaded de4dot build lives. It is kept per user rather than next to ILSpy on
	/// purpose: de4dot is GPLv3 and must not become part of what ILSpy distributes.
	/// </summary>
	public sealed class De4DotCache
	{
		public De4DotCache(string? root = null)
		{
			Root = root ?? Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
				"ILSpy", "de4dot", De4DotRelease.Version);
		}

		/// <summary>Directory holding the extracted build for the pinned version.</summary>
		public string Root { get; }

		/// <summary>Where a freshly downloaded archive is unpacked before being moved into place.</summary>
		public string NewStagingDirectory()
			=> Path.Combine(Path.GetDirectoryName(Root.TrimEnd(Path.DirectorySeparatorChar))
				?? Path.GetTempPath(), ".staging-" + Guid.NewGuid().ToString("N"));
	}
}
