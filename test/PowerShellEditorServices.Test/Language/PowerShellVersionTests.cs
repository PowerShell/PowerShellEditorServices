//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#if !CoreCLR

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class PowerShellVersionTests 
    {
        [Theory]
        [InlineData("3", "4")]
        [InlineData("4", "4")]
        [InlineData("5", "5r1")]
        public void CompilesWithPowerShellVersion(string version, string versionSuffix)
        {
            var assemblyPath = 
                Path.GetFullPath(
                    string.Format(
                        @"..\..\..\..\packages\Microsoft.PowerShell.{0}.ReferenceAssemblies.1.0.0\lib\net4\System.Management.Automation.dll", 
                        version));

            var projectPath = @"..\..\..\..\src\PowerShellEditorServices\PowerShellEditorServices.csproj";
            FileInfo fi = new FileInfo(projectPath);
            var projectVersion = Path.Combine(fi.DirectoryName, version + ".PowerShellEditorServices.csproj");

            var doc = XDocument.Load(projectPath);
            var references = doc.Root.Descendants().Where(m => m.Name.LocalName == "Reference");
            var reference = references.First(m => m.Attribute("Include").Value.StartsWith("System.Management.Automation"));
            reference.Add(new XElement("{http://schemas.microsoft.com/developer/msbuild/2003}HintPath", assemblyPath));

            doc.Save(projectVersion);

            try
            {
                Compile(projectVersion, version, versionSuffix);
            }
            finally
            {
                File.Delete(projectVersion);
            }
        }

        private void Compile(string project, string version, string versionSuffix)
        {
            string msbuild;
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\MSBuild\ToolsVersions\14.0"))
            {
                var root = key.GetValue("MSBuildToolsPath") as string;
                msbuild = Path.Combine(root, "MSBuild.exe");
            }

            FileInfo fi = new FileInfo(project);
            string solutionDir = fi.Directory.Parent.Parent.FullName;
            var outPath = Path.GetTempPath();

            var p = new Process();
            p.StartInfo.FileName = msbuild;
            p.StartInfo.Arguments = string.Format(@" {0} /p:Configuration=Debug /t:Build /fileLogger " + 
                                                  @"/flp1:logfile=errors.txt;errorsonly  /p:SolutionDir={1} " + 
                                                  @"/p:SolutionName=PowerShellEditorServices " + 
                                                  @"/p:DefineConstants=PowerShellv{2} /p:OutDir={3}", 
                                                  project, solutionDir, version, outPath);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;

            // On my system, the parent process (VS I guess), defines an environment variable - PLATFORM=X64 - 
            // which breaks the build of the versioned C# project files. Removing it fixes the issue.
            p.StartInfo.EnvironmentVariables.Remove("Platform");

            p.Start();
            p.WaitForExit(60000);
            if (!p.HasExited)
            {
                p.Kill();
                throw new Exception("Compilation didn't complete in 60 seconds.");
            }

            if (p.ExitCode != 0)
            {
                var errors = File.ReadAllText("errors.txt");
                throw new Exception(errors);
            }
        }
    }
}


#endif