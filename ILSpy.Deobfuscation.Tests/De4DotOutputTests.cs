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
	/// de4dot reports a detected obfuscator as "Detected &lt;name&gt; (&lt;file&gt;)" on stdout. An
	/// unknown obfuscator produces no such line, which is how "not obfuscated" is recognised.
	/// </summary>
	[TestFixture]
	public class De4DotOutputTests
	{
		[Test]
		public void Detected_line_yields_the_obfuscator_name()
		{
			const string output = """
				de4dotEx v3.10.0

				Detected SmartAssembly 6.9 (C:\in\a.dll)
				""";

			var result = De4DotOutput.ParseDetection(output);

			result.IsObfuscated.Should().BeTrue();
			result.Name.Should().Be("SmartAssembly 6.9");
		}

		[Test]
		public void Name_containing_parentheses_keeps_everything_before_the_last_group()
		{
			var result = De4DotOutput.ParseDetection(@"Detected Agile.NET (CliSecure) (C:\in\a.dll)");

			result.Name.Should().Be("Agile.NET (CliSecure)");
		}

		[TestCase("")]
		[TestCase("de4dotEx v3.10.0\r\n\r\nSkipping unknown obfuscator: C:\\in\\a.dll")]
		[TestCase("de4dotEx v3.10.0")]
		public void Without_a_detected_line_the_file_is_not_obfuscated(string output)
		{
			var result = De4DotOutput.ParseDetection(output);

			result.IsObfuscated.Should().BeFalse();
			result.Name.Should().BeNull();
		}

		[Test]
		public void Raw_output_is_preserved_for_the_report()
		{
			const string output = "Detected Foo (a.dll)";

			De4DotOutput.ParseDetection(output).RawOutput.Should().Be(output);
		}
	}
}
