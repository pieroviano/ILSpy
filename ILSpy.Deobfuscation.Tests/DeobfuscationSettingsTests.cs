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
using System.Xml.Linq;

using AwesomeAssertions;

using ICSharpCode.ILSpy.Deobfuscation.Core;

using NUnit.Framework;

namespace ICSharpCode.ILSpy.Deobfuscation.Tests
{
	[TestFixture]
	public class DeobfuscationSettingsTests
	{
		[Test]
		public void The_risky_options_are_off_until_asked_for()
		{
			var settings = new DeobfuscationSettings();
			settings.LoadFromXml(new XElement("DeobfuscationSettings"));

			settings.DetectOnOpen.Should().BeFalse("detection spawns a process for every assembly opened");
			settings.AllowDynamicStringDecryption.Should().BeFalse("it executes code from the analysed assembly");
			settings.RenameSymbols.Should().BeTrue("restoring names is the point of deobfuscating");
			settings.De4DotPath.Should().BeEmpty("empty means the managed cache");
			settings.PreserveTokens.Should().BeFalse();
			settings.KeepNames.Should().BeEmpty();
		}

		[Test]
		public void Settings_round_trip_through_xml()
		{
			var settings = new DeobfuscationSettings {
				De4DotPath = @"C:\tools\de4dot.exe",
				DetectOnOpen = true,
				RenameSymbols = false,
				KeepNames = "ntp",
				PreserveTokens = true,
				AllowDynamicStringDecryption = true,
			};

			var copy = new DeobfuscationSettings();
			copy.LoadFromXml(settings.SaveToXml());

			copy.De4DotPath.Should().Be(@"C:\tools\de4dot.exe");
			copy.DetectOnOpen.Should().BeTrue();
			copy.RenameSymbols.Should().BeFalse();
			copy.KeepNames.Should().Be("ntp");
			copy.PreserveTokens.Should().BeTrue();
			copy.AllowDynamicStringDecryption.Should().BeTrue();
		}

		[Test]
		public void Section_name_is_stable()
		{
			new DeobfuscationSettings().SectionName.ToString().Should().Be("DeobfuscationSettings");
		}

		[Test]
		public void Options_follow_the_settings_but_never_decrypt_strings_unless_allowed()
		{
			var settings = new DeobfuscationSettings {
				RenameSymbols = false, KeepNames = "nt", PreserveTokens = true, AllowDynamicStringDecryption = false
			};

			var options = settings.ToOptions(decryptStrings: true);

			options.Rename.Should().BeFalse();
			options.KeepNames.Should().Be("nt");
			options.PreserveTokens.Should().BeTrue();
			options.Strings.Should().Be(StringDecryption.None, "the setting gates the request");
		}

		[Test]
		public void Dynamic_decryption_needs_both_the_setting_and_the_request()
		{
			var allowed = new DeobfuscationSettings { AllowDynamicStringDecryption = true };

			allowed.ToOptions(decryptStrings: true).Strings.Should().Be(StringDecryption.Dynamic);
			allowed.ToOptions(decryptStrings: false).Strings.Should().Be(StringDecryption.None);
		}
	}

	[TestFixture]
	public class DeobfuscationServiceTests
	{
		static DeobfuscationService Create(DeobfuscationSettings settings, De4DotHost host = De4DotHost.WindowsX64)
			=> new(settings, host, new System.Net.Http.HttpClient());

		[Test]
		public void A_configured_path_is_used_as_given()
		{
			var exe = Path.Combine(Path.GetTempPath(), "ILSpyDeobf_" + Guid.NewGuid().ToString("N") + ".exe");
			File.WriteAllText(exe, "stub");
			var service = Create(new DeobfuscationSettings { De4DotPath = exe });

			service.TryResolveExecutable(out var resolved).Should().BeTrue();
			resolved.Should().Be(exe);
		}

		[Test]
		public void A_configured_path_that_does_not_exist_resolves_to_nothing()
		{
			var service = Create(new DeobfuscationSettings { De4DotPath = @"C:\nope\de4dot.exe" });

			service.TryResolveExecutable(out _).Should().BeFalse();
		}

		[Test]
		public void An_unsupported_host_never_resolves_and_offers_no_install()
		{
			var service = Create(new DeobfuscationSettings(), De4DotHost.Unsupported);

			service.IsPlatformSupported.Should().BeFalse();
			service.TryResolveExecutable(out _).Should().BeFalse();
			service.CreateInstaller().Should().BeNull("there is no de4dotEx build to install");
		}

		[Test]
		public void Output_paths_keep_the_file_name_and_never_collide()
		{
			using var service = Create(new DeobfuscationSettings());

			var first = service.CreateOutputPath(@"C:\in\Sample.dll");
			var second = service.CreateOutputPath(@"C:\in\Sample.dll");

			Path.GetFileName(first).Should().Be("Sample.dll", "the tree shows the file name");
			Path.GetFileName(second).Should().Be("Sample.dll");
			first.Should().NotBe(second, "deobfuscating twice must not overwrite the first result");
		}

		[Test]
		public void Disposing_removes_everything_it_wrote()
		{
			var service = Create(new DeobfuscationSettings());
			var path = service.CreateOutputPath(@"C:\in\Sample.dll");
			File.WriteAllText(path, "cleaned");

			service.Dispose();

			File.Exists(path).Should().BeFalse("deobfuscated copies are session scratch files");
		}
	}
}
