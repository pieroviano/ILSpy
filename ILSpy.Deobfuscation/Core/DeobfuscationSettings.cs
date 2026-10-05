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

using System.Xml.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

using ICSharpCode.ILSpyX.Settings;

namespace ICSharpCode.ILSpy.Deobfuscation.Core
{
	/// <summary>
	/// Deobfuscation settings, persisted under the <c>&lt;DeobfuscationSettings/&gt;</c> XML section.
	/// </summary>
	public sealed partial class DeobfuscationSettings : ObservableObject, ISettingsSection
	{
		/// <summary>Explicit de4dot executable; empty means the build managed in the user cache.</summary>
		[ObservableProperty]
		string de4DotPath = string.Empty;

		/// <summary>Detect the obfuscator of assemblies as they are opened.</summary>
		[ObservableProperty]
		bool detectOnOpen;

		[ObservableProperty]
		bool renameSymbols = true;

		/// <summary>de4dot symbol kinds to leave alone, e.g. "ntp"; blank keeps nothing.</summary>
		[ObservableProperty]
		string keepNames = string.Empty;

		[ObservableProperty]
		bool preserveTokens;

		/// <summary>
		/// Permits string decryption that invokes the analysed assembly's own decrypter. Off by
		/// default, and still confirmed per run, because it executes untrusted code.
		/// </summary>
		[ObservableProperty]
		bool allowDynamicStringDecryption;

		public XName SectionName => "DeobfuscationSettings";

		public void LoadFromXml(XElement e)
		{
			De4DotPath = (string?)e.Attribute(nameof(De4DotPath)) ?? string.Empty;
			DetectOnOpen = (bool?)e.Attribute(nameof(DetectOnOpen)) ?? false;
			RenameSymbols = (bool?)e.Attribute(nameof(RenameSymbols)) ?? true;
			KeepNames = (string?)e.Attribute(nameof(KeepNames)) ?? string.Empty;
			PreserveTokens = (bool?)e.Attribute(nameof(PreserveTokens)) ?? false;
			AllowDynamicStringDecryption = (bool?)e.Attribute(nameof(AllowDynamicStringDecryption)) ?? false;
		}

		public XElement SaveToXml()
		{
			var section = new XElement(SectionName);
			section.SetAttributeValue(nameof(De4DotPath), De4DotPath);
			section.SetAttributeValue(nameof(DetectOnOpen), DetectOnOpen);
			section.SetAttributeValue(nameof(RenameSymbols), RenameSymbols);
			section.SetAttributeValue(nameof(KeepNames), KeepNames);
			section.SetAttributeValue(nameof(PreserveTokens), PreserveTokens);
			section.SetAttributeValue(nameof(AllowDynamicStringDecryption), AllowDynamicStringDecryption);
			return section;
		}

		/// <param name="decryptStrings">What the user asked for on this run; dynamic decryption also
		/// requires <see cref="AllowDynamicStringDecryption"/>, so the setting is the gate.</param>
		public DeobfuscationOptions ToOptions(bool decryptStrings)
			=> new(
				Rename: RenameSymbols,
				KeepNames: KeepNames,
				PreserveTokens: PreserveTokens,
				Strings: decryptStrings && AllowDynamicStringDecryption ? StringDecryption.Dynamic : StringDecryption.None);
	}
}
