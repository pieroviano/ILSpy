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

using System.Composition;
using System.Xml.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Deobfuscation.Core;
using ICSharpCode.ILSpy.Options;

namespace ICSharpCode.ILSpy.Deobfuscation.UI
{
	/// <summary>
	/// The Symbols-style settings panel for deobfuscation. It also drives installing de4dotEx, since
	/// that is the one action a user needs before anything else works.
	/// </summary>
	[ExportOptionPage(Order = 45)]
	[Shared]
	public sealed partial class DeobfuscationOptionsViewModel : ObservableObject, IOptionPage
	{
		[ObservableProperty]
		DeobfuscationSettings settings = null!;

		[ObservableProperty]
		string status = string.Empty;

		[ObservableProperty]
		bool isBusy;

		public string Title => "Deobfuscation";

		/// <summary>The consent text shown before anything is downloaded.</summary>
		public string InstallDescription => DeobfuscationHost.Instance.DescribeInstall();

		public void Load(SettingsService service)
		{
			Settings = service.GetSettings<DeobfuscationSettings>();
			RefreshStatus();
		}

		public void LoadDefaults()
		{
			Settings.LoadFromXml(new XElement("DeobfuscationSettings"));
			RefreshStatus();
		}

		void RefreshStatus()
			=> Status = DeobfuscationHost.Instance.DescribeStatus();

		[RelayCommand]
		async System.Threading.Tasks.Task InstallAsync()
		{
			IsBusy = true;
			try
			{
				Status = await DeobfuscationHost.Instance.InstallAsync();
			}
			finally
			{
				IsBusy = false;
			}
		}
	}
}
