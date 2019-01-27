using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using System.Linq;
using System.Collections.Generic;

namespace Gekka.VisualStudio.Extension.CoverageTools
{
    internal sealed class MergeCoverageCommand
    {
        public const int CommandId = PackageIds.MergeCoverageCommandId; //0x0100;
        public static readonly Guid CommandSet = new Guid(PackageGuids.guidMergeCoverageCommandPackageCmdSetString); //new Guid("dce419bc-d1c0-43b3-ab20-28547a442ece");

        private readonly AsyncPackage package;

        public static MergeCoverageCommand Instance { get; private set; }

        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get { return this.package; }
        }


        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new MergeCoverageCommand(package, commandService);
        }

        private MergeCoverageCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        private async void Execute(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await this.ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            if (dte == null)
            {
                return;
            }
            try
            {
                var w = dte.Windows.Item(CoverageInfo.CodecoverageWindowGuidString);
                w.Activate();
            }
            catch
            {
                VsShellUtilities.ShowMessageBox(this.package, "Can't open CodeCoverage Window", "Merge Coverage File", OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            string[] folders;
            using (var dlg = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog())
            {
                dlg.Title = "Select folders containg .coverage.";
                dlg.IsFolderPicker = true;
                dlg.Multiselect = true;
                if (dlg.ShowDialog() != Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                {
                    return;
                }

                folders = dlg.FileNames.ToArray();
            }

            List<string> files = new List<string>();
            string output;
            foreach (string folder in folders)
            {
                files.AddRange(System.IO.Directory.GetFiles(folder, "*" + CoverageInfo.CoverageFileExtention, System.IO.SearchOption.TopDirectoryOnly));
            }

            if (files.Count == 0)
            {
                return;
            }

            using (var dlg = new Microsoft.WindowsAPICodePack.Dialogs.CommonSaveFileDialog())
            {
                dlg.Title = "Select output file";
                dlg.AlwaysAppendDefaultExtension = true;
                dlg.DefaultExtension = CoverageInfo.CoverageFileExtention;
                dlg.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("Coverage File", "*" + CoverageInfo.CoverageFileExtention));
                dlg.OverwritePrompt = true;


                if (dlg.ShowDialog() != Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                {
                    return;
                }
                output = dlg.FileName;
            }

            try
            {
                var ci = new CoverageInfo();
                ci.MergeCoverageFiles(files, output, true);
                //ci.LoadCoverageFileToVS(output, dte);
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(this.package, ex.Message, "Merge Coverage File", OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }
        }
    }
}