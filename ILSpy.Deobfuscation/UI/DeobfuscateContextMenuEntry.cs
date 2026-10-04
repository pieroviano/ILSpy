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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.ILSpy.Deobfuscation.Core;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpyX;

namespace ICSharpCode.ILSpy.Deobfuscation.UI
{
	/// <summary>
	/// Right-click an assembly -> "Deobfuscate". Runs de4dot over the file and opens the cleaned copy
	/// alongside the original, which is never modified.
	/// </summary>
	[ExportContextMenuEntry(Header = "Deobfuscate", Category = "Debug", Order = 430)]
	[Shared]
	public sealed class DeobfuscateContextMenuEntry : IContextMenuEntry
	{
		readonly DockWorkspace dockWorkspace;

		[ImportingConstructor]
		public DeobfuscateContextMenuEntry(DockWorkspace dockWorkspace)
		{
			this.dockWorkspace = dockWorkspace;
		}

		public bool IsEnabled(TextViewContext context) => true;

		// Hidden outright where de4dotEx has no build: the entry never appears rather than failing
		// when it is used.
		public bool IsVisible(TextViewContext context)
			=> DeobfuscationHost.IsPlatformSupported && GetAssembly(context) != null;

		public void Execute(TextViewContext context)
		{
			if (GetAssembly(context) is { } assembly)
				DeobfuscationActions.RunAsync(assembly, dockWorkspace).HandleExceptions();
		}

		internal static LoadedAssembly? GetAssembly(TextViewContext context)
			=> context.SelectedTreeNodes is { Length: 1 } nodes
				&& nodes[0] is AssemblyTreeNode { LoadedAssembly.IsLoadedAsValidAssembly: true } node
					? node.LoadedAssembly
					: null;
	}

	/// <summary>
	/// Right-click an assembly -> "Detect obfuscator". Reports what de4dot recognises without
	/// writing anything.
	/// </summary>
	[ExportContextMenuEntry(Header = "Detect obfuscator", Category = "Debug", Order = 431)]
	[Shared]
	public sealed class DetectObfuscatorContextMenuEntry : IContextMenuEntry
	{
		readonly DockWorkspace dockWorkspace;

		[ImportingConstructor]
		public DetectObfuscatorContextMenuEntry(DockWorkspace dockWorkspace)
		{
			this.dockWorkspace = dockWorkspace;
		}

		public bool IsEnabled(TextViewContext context) => true;

		public bool IsVisible(TextViewContext context)
			=> DeobfuscationHost.IsPlatformSupported && DeobfuscateContextMenuEntry.GetAssembly(context) != null;

		public void Execute(TextViewContext context)
		{
			if (DeobfuscateContextMenuEntry.GetAssembly(context) is { } assembly)
				DeobfuscationActions.DetectAsync(assembly, dockWorkspace).HandleExceptions();
		}
	}
}
