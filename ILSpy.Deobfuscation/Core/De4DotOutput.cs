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
using System.Text.RegularExpressions;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	/// <param name="Name">The obfuscator de4dot reported, or <c>null</c> when it recognised none.</param>
	public sealed record ObfuscatorDetectionResult(string? Name, string RawOutput)
	{
		public bool IsObfuscated => Name != null;
	}

	/// <summary>Reads what de4dot printed.</summary>
	public static partial class De4DotOutput
	{
		// "Detected <name> (<file>)". The name may itself contain parentheses, as in
		// "Agile.NET (CliSecure)", so the trailing file group is matched against the end of the line
		// and the name takes everything before it.
		[GeneratedRegex(@"^Detected\s+(?<name>.+)\s+\([^()]*\)\s*$", RegexOptions.Multiline)]
		private static partial Regex DetectedLine();

		public static ObfuscatorDetectionResult ParseDetection(string output)
		{
			ArgumentNullException.ThrowIfNull(output);
			var match = DetectedLine().Match(output);
			return new ObfuscatorDetectionResult(match.Success ? match.Groups["name"].Value.Trim() : null, output);
		}
	}
}
