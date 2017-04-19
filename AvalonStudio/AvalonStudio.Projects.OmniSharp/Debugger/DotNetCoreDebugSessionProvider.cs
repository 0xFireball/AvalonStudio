﻿namespace AvalonStudio.Debugging.DotNetCore
{
    using AvalonStudio.Extensibility;
    using AvalonStudio.GlobalSettings;
    using AvalonStudio.Platforms;
    using AvalonStudio.Projects;
    using AvalonStudio.Projects.OmniSharp;
    using Mono.Debugging.Client;
    using Mono.Debugging.Win32;
    using System.IO;

    public class DotNetCoreDebugger : IDebugger2
    {
        public void Activation()
        {
        }

        public void BeforeActivation()
        {
            IoC.RegisterConstant<DotNetCoreDebugger>(this);
        }

        public DebuggerSession CreateSession(IProject project)
        {
            var settings = SettingsBase.GetSettings<DotNetToolchainSettings>();

            var dbgShimPath = Path.Combine(Path.GetDirectoryName(settings.DotNetPath), "shared", "Microsoft.NETCore.App", "2.0.0-preview1-002021-00", "dbgshim" + Platform.DLLExtension);

            var result = new CoreClrDebuggerSession(System.IO.Path.GetInvalidPathChars(), dbgShimPath);

            result.CustomSymbolReaderFactory = new PdbSymbolReaderFactory();

            return result;
        }

        public DebuggerSessionOptions GetDebuggerSessionOptions(IProject project)
        {
            var evaluationOptions = EvaluationOptions.DefaultOptions.Clone();

            evaluationOptions.EllipsizeStrings = false;
            evaluationOptions.GroupPrivateMembers = false;
            evaluationOptions.EvaluationTimeout = 1000;

            return new DebuggerSessionOptions() { EvaluationOptions = evaluationOptions };
        }

        public DebuggerStartInfo GetDebuggerStartInfo(IProject project)
        {
            var startInfo = new DebuggerStartInfo()
            {
                Command = "dotnet" + Platforms.Platform.ExecutableExtension,
                Arguments = project.Executable,
                WorkingDirectory = System.IO.Path.GetDirectoryName(project.Executable),
                UseExternalConsole = false,
                CloseExternalConsoleOnExit = true
            };

            return startInfo;
        }

        public object GetSettingsControl(IProject project)
        {
            return null;
        }
    }
}