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
using System.Linq;
using System.Reflection;

using AwesomeAssertions;

using ICSharpCode.ILSpy.Deobfuscation.Core;
using ICSharpCode.ILSpy.Deobfuscation.UI;

using NUnit.Framework;

namespace ICSharpCode.ILSpy.Deobfuscation.Tests
{
	/// <summary>
	/// ILSpy discovers plugins by scanning for *.Plugin.dll and reading MEF attributes, so a wrong
	/// assembly name or a missing export makes the whole feature silently absent.
	/// </summary>
	[TestFixture]
	public class PluginCompositionTests
	{
		static readonly Assembly Plugin = typeof(DeobfuscateContextMenuEntry).Assembly;

		[Test]
		public void The_assembly_is_named_so_the_plugin_scan_finds_it()
		{
			Plugin.GetName().Name.Should().EndWith(".Plugin", "AppComposition only loads *.Plugin.dll");
		}

		[TestCase(typeof(DeobfuscateContextMenuEntry), "Deobfuscate")]
		[TestCase(typeof(DetectObfuscatorContextMenuEntry), "Detect obfuscator")]
		public void Context_menu_entries_are_exported_under_the_debug_category(Type entry, string header)
		{
			var attribute = entry.GetCustomAttributes()
				.Single(a => a.GetType().Name == "ExportContextMenuEntryAttribute");

			Read(attribute, "Header").Should().Be(header);
			Read(attribute, "Category").Should().Be("Debug", "it sits with Select PDB and the symbol entries");
		}

		[Test]
		public void The_options_page_is_exported_after_the_built_in_pages()
		{
			var attribute = typeof(DeobfuscationOptionsViewModel).GetCustomAttributes()
				.Single(a => a.GetType().Name == "ExportOptionPageAttribute");

			Convert.ToInt32(Read(attribute, "Order")).Should().BeGreaterThan(40,
				"ReadyToRun uses 40; this page follows the built-in ones");
		}

		[Test]
		public void Nothing_outside_the_plugin_is_needed_to_decide_availability()
		{
			// The gate must not touch the MEF container: it is read while building context menus,
			// including in hosts where the plugin is present but unusable.
			var act = () => DeobfuscationHost.IsPlatformSupported;

			act.Should().NotThrow();
		}

		[Test]
		public void The_pinned_release_is_the_one_the_installer_would_fetch()
		{
			var asset = De4DotRelease.ForHost(De4DotHost.WindowsX64)!;

			asset.Url.Should().Contain(De4DotRelease.Version);
			asset.Url.Should().StartWith("https://", "the download must not happen over plain HTTP");
		}

		static object? Read(Attribute attribute, string property)
			=> attribute.GetType().GetProperty(property)!.GetValue(attribute);
	}
}
