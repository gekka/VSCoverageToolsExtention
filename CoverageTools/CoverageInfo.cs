using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gekka.VisualStudio.Extension.CoverageTools
{
    partial class CoverageInfo
    {
        public const string CoverageFileExtention = ".coverage";
        public const string CodecoverageWindowGuidString = "{905DA7D1-18FD-4A46-8D0F-A5FF58ADA9DE}";

        private System.Reflection.MethodInfo miMergeCoverageFile;

        public void Initialize()
        {
            if (miMergeCoverageFile != null)
            {
                return;
            }

            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(_ => _.GetName().Name == "Microsoft.VisualStudio.Coverage.Analysis");

            if (asm == null)
            {
                throw new ApplicationException("Coverage Module is not loading.");
            }

            Type t = asm.GetType("Microsoft.VisualStudio.Coverage.Analysis.CoverageInfo", false);
            var mi = t?.GetMethod
                        ("MergeCoverageFiles"
                        , System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                        , null, new Type[] { typeof(string), typeof(string), typeof(string), typeof(bool) }
                        , null);
            if (t == null || mi == null)
            {
                throw new ApplicationException("Not found Merge function.");
            }
            miMergeCoverageFile = mi;
        }

        public void MergeCoverageFiles(string firstCoverageFile, string secondCoverageFile, string outputCoverageFile, bool overwriteOutputFile = false)
        {
            Initialize();

            miMergeCoverageFile.Invoke(null, new object[] { firstCoverageFile, secondCoverageFile, outputCoverageFile, overwriteOutputFile });
        }

        public void MergeCoverageFiles(IEnumerable<string> coverageFiles, string outputCoverageFile, bool overwriteOutputFile = false)
        {
            Initialize();

            if (coverageFiles.Any(_ => string.Equals(_, outputCoverageFile, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new ArgumentException(nameof(outputCoverageFile));
            }
            string firstCoverageFile = coverageFiles.FirstOrDefault();
            if (firstCoverageFile == null)
            {
                return;
            }
            System.IO.File.Copy(firstCoverageFile, outputCoverageFile, overwriteOutputFile);

            string[] files = coverageFiles.Skip(1).ToArray();
            if (files.Length == 0)
            {
                return;
            }

            string temp1 = GetTempFile();
            string temp2 = GetTempFile();

            try
            {
                System.IO.File.Copy(firstCoverageFile, temp1, true);
                string tempinput;
                string tempoutput = firstCoverageFile;

                foreach (string file in files)
                {
                    tempinput = tempoutput;
                    tempoutput = tempoutput == temp1 ? temp2 : temp1;
                    miMergeCoverageFile.Invoke(null, new object[] { tempinput, file, tempoutput, true });
                }
                System.IO.File.Copy(tempoutput, outputCoverageFile, overwriteOutputFile);
            }
            finally
            {
                System.IO.File.Delete(temp1);
                System.IO.File.Delete(temp2);
            }
        }

        private static string GetTempFile()
        {
            string temp = System.IO.Path.GetTempFileName();
            string tempx = System.IO.Path.ChangeExtension(temp, "coverage");
            System.IO.File.Move(temp, System.IO.Path.Combine(temp, tempx));
            return tempx;
        }
    }
}
