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
	/// De4DotArguments is the only place that knows de4dot's command line, so every option the UI
	/// offers is pinned to the flag it produces.
	/// </summary>
	[TestFixture]
	public class De4DotArgumentTests
	{
		static DeobfuscationOptions Default => new();

		[Test]
		public void Detect_passes_the_file_and_asks_de4dot_to_exit()
		{
			De4DotArguments.Detect(@"C:\in\a.dll").Should().Equal("-d", "-f", @"C:\in\a.dll");
		}

		[Test]
		public void Deobfuscate_always_names_input_and_output_explicitly()
		{
			// Never rely on de4dot's default "-cleaned" naming: the caller owns the output path.
			De4DotArguments.Deobfuscate(@"C:\in\a.dll", @"C:\out\a.dll", Default)
				.Should().ContainInOrder("-f", @"C:\in\a.dll", "-o", @"C:\out\a.dll");
		}

		[Test]
		public void Renaming_is_on_by_default_and_suppressed_with_dont_rename()
		{
			De4DotArguments.Deobfuscate("a", "b", Default).Should().NotContain("--dont-rename");
			De4DotArguments.Deobfuscate("a", "b", Default with { Rename = false }).Should().Contain("--dont-rename");
		}

		[Test]
		public void Keep_names_and_preserve_tokens_are_passed_when_set()
		{
			var args = De4DotArguments.Deobfuscate("a", "b", Default with { KeepNames = "ntp", PreserveTokens = true });

			args.Should().ContainInOrder("--keep-names", "ntp");
			args.Should().Contain("--preserve-tokens");
		}

		[Test]
		public void Blank_keep_names_is_omitted()
		{
			De4DotArguments.Deobfuscate("a", "b", Default with { KeepNames = "   " }).Should().NotContain("--keep-names");
		}

		[Test]
		public void String_decryption_is_off_unless_asked_for()
		{
			De4DotArguments.Deobfuscate("a", "b", Default).Should().NotContain("--strtyp");
		}

		[Test]
		public void Dynamic_string_decryption_selects_the_delegate_decrypter()
		{
			De4DotArguments.Deobfuscate("a", "b", Default with { Strings = StringDecryption.Dynamic })
				.Should().ContainInOrder("--strtyp", "delegate");
		}

		[Test]
		public void Forced_obfuscator_type_is_passed_with_p()
		{
			De4DotArguments.Deobfuscate("a", "b", Default with { ForcedObfuscatorType = "sa" })
				.Should().ContainInOrder("-p", "sa");
		}
	}
}
