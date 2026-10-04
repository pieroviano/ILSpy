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

using System.Collections.Generic;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	/// <summary>
	/// Builds de4dot's command line. The only place in the plugin that knows the tool's flags, so the
	/// contract can be pinned by tests without running anything.
	/// </summary>
	public static class De4DotArguments
	{
		/// <summary>Detect the obfuscator and exit without writing anything.</summary>
		public static IReadOnlyList<string> Detect(string inputFile) => new[] { "-d", "-f", inputFile };

		public static IReadOnlyList<string> Deobfuscate(string inputFile, string outputFile, DeobfuscationOptions options)
		{
			// -f/-o are always explicit: de4dot otherwise invents a "-cleaned" name next to the input,
			// and the caller owns where the result goes.
			var args = new List<string> { "-f", inputFile, "-o", outputFile };
			if (!options.Rename)
				args.Add("--dont-rename");
			if (!string.IsNullOrWhiteSpace(options.KeepNames))
			{
				args.Add("--keep-names");
				args.Add(options.KeepNames!.Trim());
			}
			if (options.PreserveTokens)
				args.Add("--preserve-tokens");
			if (options.Strings == StringDecryption.Dynamic)
			{
				args.Add("--strtyp");
				args.Add("delegate");
			}
			if (!string.IsNullOrWhiteSpace(options.ForcedObfuscatorType))
			{
				args.Add("-p");
				args.Add(options.ForcedObfuscatorType!.Trim());
			}
			return args;
		}
	}
}
