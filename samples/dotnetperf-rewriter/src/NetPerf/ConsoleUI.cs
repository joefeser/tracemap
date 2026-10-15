using System;
using System.Linq;
using NetPerf.Common;
using System.IO;
using NetPerf.Common.Instrumentation;
using System.Text.RegularExpressions;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using NetPerf.Core;
using NetPerf.Core.Interfaces;
using System.Collections.Generic;

namespace NetPerf {

	public static class ConsoleUI {

		private static bool _errorDuringExecution;

		public static bool ErrorDuringExecution {
			get {
				return _errorDuringExecution;
			}
			set {
				_errorDuringExecution = value;
			}
		}

		public static void Main(string[] args) {
			var sw = new Stopwatch();

			var pauseOnError = false;

			try {
				ConsoleStub.WriteLine(string.Empty);
				ConsoleStub.Write("DotNetPerf Timing Console v");
				ConsoleStub.WriteLine(VersionInformation.GetFileVersion(System.Reflection.Assembly.GetExecutingAssembly()));
				ConsoleStub.WriteLine(VersionInformation.Copyright(System.Reflection.Assembly.GetExecutingAssembly()));
				ConsoleStub.WriteLine("http://www.dotnetperf.com or follow @dotnetperf for more information.");
				ConsoleStub.WriteLine(string.Empty);
				ConsoleStub.WriteLine(string.Empty);

				AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

				sw.Start();

				// * multiple instrument commands are supported
				// * the order of interpretation is
				//   help command
				//   listreferences command
				//   restore command(s)
				//   instrument command(s)
				//   run external process command
				//   create reports command

				var results = NetPerfCommandLineParser.Parse(args);
				if (results.HasError) {
					ConsoleStub.WriteLine(results.ErrorMessage);
					//ConsoleStub.ReadLine();
					return;
				}

				ConsoleStub.WriteLine("args passed:" + string.Join(" ", args));

				var helpCmd = (HelpCommand)results.Data
					.FirstOrDefault(item => item is HelpCommand);

				var listRefCmds = from cmd in results.Data
								  where cmd is ListLocalReferencesCommand
								  select (ListLocalReferencesCommand)cmd;

				var instrumentCmds = from cmd in results.Data
									 where cmd is InstrumentCommand && !(cmd is RestoreCommand)
									 select (InstrumentCommand)cmd;

				var restoreCmds = from cmd in results.Data
								  where cmd is RestoreCommand
								  select (RestoreCommand)cmd;

				var runProcessCmds = from cmd in results.Data
									 where cmd is RunExternalProcessCommand
									 select (RunExternalProcessCommand)cmd;

				var createReportCmds = from cmd in results.Data
									   where cmd is CreateReportCommand
									   select (CreateReportCommand)cmd;

				var instrumentPoints = (from cmd in results.Data
										where cmd is PointsCommand
										select (PointsCommand)cmd).FirstOrDefault();

				var pauseOnErrorCommand = (from cmd in results.Data
										   where cmd is PauseOnErrorCommand
										   select (PauseOnErrorCommand)cmd).FirstOrDefault();

				pauseOnError = pauseOnErrorCommand != null;

				if (helpCmd != null) {
					helpCmd.DisplayHelp();
					return;
				}

				if (listRefCmds.Any()) {
					foreach (var cmd in listRefCmds) {
						var references = cmd.FindAllReferences();
						for (int i = 0; i < references.Count; i++) {
							Console.WriteLine(references[i]);
						}
					}
					return;
				}

				if (restoreCmds.IsNotEmpty()) {
					foreach (var cmd in restoreCmds) {
						foreach (var originalAssemblyPath in cmd.AssemblyPaths) {
							var assembly = originalAssemblyPath.IsAbsolute ? originalAssemblyPath : originalAssemblyPath.ToAbsolutePath(cmd.BaseDirectory);
							var assemblySymbols = assembly.ChangeExtension(".pdb");

							var backupDir = cmd.GetBackupDirForAssembly(assembly);
							if (!DirectoryStub.Exists(backupDir.FullPath)) {
								ConsoleStub.WriteLine("The backup directory '{0}' does not exist.", backupDir.FullPath);
								ConsoleStub.WriteLine("The assembly '{0}' will not be restored.", assembly.FileName);
								continue;
							}

							if (FileStub.Exists(assembly.ChangeParentDirectory(backupDir))) {
								FileStub.Copy(assembly.ChangeParentDirectory(backupDir), assembly, true);
								ConsoleStub.WriteLine("Assembly '{0}' has been restored to{1}    '{2}'", assembly.FileName, EnvironmentStub.NewLine, assembly.ParentDirectory);
							}
							else {
								ConsoleStub.WriteLine("Assembly '{0}' was not restored because it was not in the backup directory.", assembly.FileName);
							}

							if (FileStub.Exists(assemblySymbols.ChangeParentDirectory(backupDir))) {
								FileStub.Copy(assemblySymbols.ChangeParentDirectory(backupDir), assemblySymbols, true);
								ConsoleStub.WriteLine("Symbols for assembly '{0}' have been restored to{1}    '{2}'", assembly.FileName, EnvironmentStub.NewLine, assemblySymbols.ParentDirectory);
							}
							else {
								ConsoleStub.WriteLine("Symbols for assembly '{0}' were not restored because they were not in the backup directory.", assembly.FileName);
							}
						}
					}
					sw.Stop();
					ConsoleStub.WriteLine("Total Time Taken: " + sw.ElapsedMilliseconds + " ms");
					return;
				}

				var instrumentCmd = instrumentCmds.SingleOrDefault();
				if (instrumentCmd != null) {

					if (instrumentPoints != null) {
						instrumentCmd.InstrumentPoints = true;
					}

					var result = ExecuteInstrumentationCommand(instrumentCmd);

					ConsoleStub.WriteLine();
					if (result.Factory.Assemblies.Count > 0) {
						result.Factory.Save(FilePath.Parse("saved.xml"));
						ConsoleStub.WriteLine("Instrumentation data stored in 'saved.xml'");
					}
					else {
						if (!result.AssembliesAlreadyInstrumented) {
							ErrorDuringExecution = true;
						}
						ConsoleStub.WriteLine("No assemblies were instrumented.");
					}

					ConsoleStub.WriteLine(string.Empty);

					if (result.Factory.Assemblies.Count > 0 || result.AssembliesAlreadyInstrumented) {
						NamedPipeMessageProcessor processor = null;
						var runProcessCmd = runProcessCmds.SingleOrDefault();
						if (runProcessCmd != null) {
							CopyRequiredAssemblies(instrumentCmd, runProcessCmd);
							ConsoleStub.WriteLine("Call External Process - Start: " + DateTime.Now.ToString());
							processor = ExecuteRunProcessCommand(runProcessCmd);
							ConsoleStub.WriteLine("Call External Process - End: " + DateTime.Now.ToString());
						}

						if (createReportCmds.Count() > 0) {

							if (processor.MethodEvents != null && processor.MethodEvents.Count > 0) {

								var visitor = new TimingVisitorBase(processor, processor);

								var timing = visitor.GetTimingData();

								//var total = timing.TimingElements.Sum(item => item.TotalTime);
								//var methodTotal = timing.TimingElements.Sum(item => item.MethodTime);
								//var childTotal = timing.TimingElements.Sum(item => item.ChildTime);
								//var myTotal = methodTotal + childTotal;

								//only cast once
								var totalMethodTime = (double)timing.TotalMethodTime;
								var totalChildTime = (double)timing.TotalChildTime;
								var first = timing.TimingElements.First();
								var methodTime = (float)((double)first.MethodTime / totalMethodTime);
								var childTime = (float)((double)first.ChildTime / totalChildTime);

								//JIT and timing overhead can be calculated by this number.
								//I guess it depends on how you want this number. TotalExecutionTimeTaken is from the first mark till the last
								//and TotalTimeWithChildren is the total time taken within methods that are tracked. Other items calling us
								//could change this number or jit time or overhead of the app itself.
								//1 - ((double)timing.TotalTimeWithChildren  / (double)timing.TotalExecutionTimeTaken)	0.11711028109702715	double

								foreach (var reportCmd in createReportCmds) {
									try {
										reportCmd.CreateReport(processor, timing);
									}
									catch (Exception ex) {
										ErrorDuringExecution = true;
										LoggingHelper.LogAndOutputError("Failed to write report:" + reportCmd.OutputFile.ToString() + " Reason:" + ex.ToString());
									}
								}
							}
							else {
								Debug.WriteLine("Method Events Did not exist. No Reports to create. Please verify that you have instrumented the assemblies that were tested.");
								ConsoleStub.WriteLine(string.Empty);
								ConsoleStub.WriteLine("Method Events Did not exist. No Reports to create. Please verify that you have instrumented the assemblies that were tested.");
								ConsoleStub.WriteLine(string.Empty);
							}
						}
					}
				}
				else {
					//there are no files to instrument and we were not able to continue.
					ErrorDuringExecution = true;
					ConsoleStub.WriteLine(string.Empty);
					ConsoleStub.WriteLine("Error: You must instrument at least one assembly.");
					ConsoleStub.WriteLine(string.Empty);
				}
			}
			catch (Exception ex) {
				ErrorDuringExecution = true;
				LoggingHelper.LogAndOutputError("General Failure that was not caught. Reason:" + ex.ToString());
			}
			sw.Stop();
			ConsoleStub.WriteLine("Total Time Taken: " + sw.ElapsedMilliseconds + " ms");

			if (ErrorDuringExecution) {
				ConsoleStub.WriteLine("");
				ConsoleStub.WriteLine("");
				ConsoleStub.WriteLine("Error during execution. Please read output and press return when complete");
			}

			if (ErrorDuringExecution && pauseOnError) {
				Console.ReadLine();
			}

		}

		private static void CopyRequiredAssemblies(InstrumentCommand instrument, RunExternalProcessCommand command) {

			//assumes that netperf.exe is in the same folder as the client assembly
			var netperfDir = NetperfHelpers.NetperfFolder();

			//we don't need to report program files as an error for anything other than the assemblies that are being instrumented
			//if we can't copy the files to the nunit or mstest folders then no big deal.

			Func<string, bool> programFilesCheck = (path) => {
				return path == null || !DirectyHasFileCreatePermission(path) || path.IndexOf(@"\Program Files", StringComparison.InvariantCultureIgnoreCase) == -1;
			};

			if (DirectyHasFileCreatePermission(command.GetExecutingFolder())) {
				CopyAssembliesToFolder(netperfDir, command.GetExecutingFolder(), programFilesCheck);
			}
			else {
				CopyRequiredAssembliesOutputAccessWarning(command.GetExecutingFolder());
			}

			if (command.ApplicationBasePath != null) {
				if (DirectyHasFileCreatePermission(command.ApplicationBasePath)) {
					CopyAssembliesToFolder(netperfDir, command.ApplicationBasePath, programFilesCheck);
				}
				else {
					CopyRequiredAssembliesOutputAccessWarning(command.ApplicationBasePath);
				}
			}

			//we need to also copy the assemblies into any folder that contains an instrumented assembly.
			foreach (var assembly in instrument.AssemblyPaths) {
				//always report this as an issue.
				CopyAssembliesToFolder(netperfDir, assembly.ParentDirectory, (value) => {
					return true;
				});
			}
		}

		private static void CopyRequiredAssembliesOutputAccessWarning(string path) {
			ConsoleStub.WriteLine("\r\n***Warning***\r\n");
			ConsoleStub.WriteLine(string.Format("Application execution directory does not have write permission.\r\n"
				+ "Are you instrumenting assemblies in: '{0}'?", path));
			ConsoleStub.WriteLine("");
		}

		private static bool DirectyHasFileCreatePermission(string p) {
			var path = PathStub.Combine(p, Guid.NewGuid().ToString() + ".tmp");
			try {
				using (var file = FileStub.OpenWrite(path)) {
					file.WriteByte(34);
				}
				FileStub.Delete(path);
			}
			catch (Exception) {
				return false;
			}
			return true;
		}

		private static List<string> _pathsCopied = new List<string>();

		private static void CopyAssembliesToFolder(string currentFolder, string destinationFolder, Func<string, bool> reportError) {

			var files = new string[] { "NetPerf.Client.dll", "Netperf.Core.dll", "Netperf.Core.Interfaces.dll" };
			try {
				//TODO create a file helper to perform these actions.
				//trim off n ending \ marks
				while (currentFolder.EndsWith("\\")) {
					currentFolder = currentFolder.Substring(0, currentFolder.Length - 1);
				}
				//trim off n ending \ marks
				while (destinationFolder.EndsWith("\\")) {
					destinationFolder = destinationFolder.Substring(0, destinationFolder.Length - 1);
				}
				var sourcePath = FilePath.Parse(currentFolder);
				var outputPath = FilePath.Parse(destinationFolder);
				if (!sourcePath.IsAbsolute) {
					sourcePath = sourcePath.ToAbsolutePath();
				}
				if (!outputPath.IsAbsolute) {
					outputPath = outputPath.ToAbsolutePath();
				}

				if (_pathsCopied.Contains(outputPath.ToString().ToUpperInvariant())) {
					//Do not tell people we already copied here, just move on.
					return;
				}

				Debug.WriteLine(string.Format("Copy Files from source to folder:{0} {1} - Start", currentFolder, destinationFolder));
				_pathsCopied.Add(outputPath.ToString().ToUpperInvariant());

				foreach (var file in files) {
					try {
						var source = PathStub.Combine(sourcePath, file);
						var destination = PathStub.Combine(outputPath, file);
						Debug.WriteLine(string.Format("Copy:{0} {1}", source, destination));
						//TODO Review #105: see if we have a gated copy command
						FileStub.Copy(source, destination, true);
					}
					catch (Exception ex) {
						if (reportError(PathStub.Combine(outputPath, file))) {
							ErrorDuringExecution = true;
							LoggingHelper.LogAndOutputError(string.Format("Error during copy of required assembly {0}. Reason:{1}. Try running as Administrator.",
								file, ex.Message));
						}
					}
				}
			}
			catch (Exception ex) {
				ErrorDuringExecution = true;
				LoggingHelper.LogAndOutputError(string.Format("Error parsing output path to copy of required assembly From:{0} To {1}. Reason:{2}",
					currentFolder, destinationFolder, ex.ToString()));
				return;
			}
			Debug.WriteLine(string.Format("Copy Files from source to folder:{0} {1} - End", currentFolder, destinationFolder));
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
			Debug.WriteLine("UnhandledException. Failure was not caught. Reason:" + e.ExceptionObject.ToString());
		}

		private static NamedPipeMessageProcessor ExecuteRunProcessCommand(RunExternalProcessCommand runProcessCmd) {
			var retVal = new NamedPipeMessageProcessor();

			using (var pipe = new NamedPipesIMessageProcessorReceiverV1(retVal, CoreConstants.PIPE_NAME, 1024, 4096)) {
				try {
					pipe.Start();
					var procInfo = new ProcessStartInfo(runProcessCmd.ApplicationToRun, runProcessCmd.ApplicationParameters);
					if (runProcessCmd.ApplicationBasePath != null) {
						procInfo.WorkingDirectory = runProcessCmd.ApplicationBasePath;
					}
					else {
						var fi = new FileInfo(runProcessCmd.ApplicationToRun);
						procInfo.WorkingDirectory = fi.DirectoryName;
					}
					ConsoleStub.WriteLine("Waiting for '" + runProcessCmd.ApplicationToRun.FileName + "' ...");
					var proc = Process.Start(procInfo);
					proc.WaitForExit();
					pipe.Stop();
				}
				catch (Exception ex) {
					ErrorDuringExecution = true;
					LoggingHelper.LogAndOutputError(ex.ToString());
				}
				return retVal;
			}
		}

		#region future for redirect output

		/*
		 
		 var procInfo = new ProcessStartInfo(runProcessCmd.ApplicationToRun, runProcessCmd.ApplicationParameters);
					if (runProcessCmd.ApplicationBasePath != null) {
						procInfo.WorkingDirectory = runProcessCmd.ApplicationBasePath;
					}
					else {
						var fi = new FileInfo(runProcessCmd.ApplicationToRun);
						procInfo.WorkingDirectory = fi.DirectoryName;
					}

					var proc = new Process();
					proc.StartInfo = procInfo;
					proc.StartInfo.RedirectStandardOutput = true;
					proc.StartInfo.RedirectStandardError = true;
					proc.EnableRaisingEvents = true;
					proc.StartInfo.CreateNoWindow = true;
					proc.StartInfo.UseShellExecute = false;
					// see below for output handler
					proc.ErrorDataReceived += new DataReceivedEventHandler(proc_ErrorDataReceived);
					proc.OutputDataReceived += new DataReceivedEventHandler(proc_OutputDataReceived);

					ConsoleStub.WriteLine("Waiting for '" + runProcessCmd.ApplicationToRun.FileName + "' ...");


					
					//var proc = new Process();
					//proc.StartInfo.FileName = cmdPath;

					//// set up output redirection
					//proc.StartInfo.RedirectStandardOutput = true;
					//proc.StartInfo.RedirectStandardError = true;
					//proc.EnableRaisingEvents = true;
					//proc.StartInfo.CreateNoWindow = true;
					//// see below for output handler
					//proc.ErrorDataReceived += proc_DataReceived;
					//proc.OutputDataReceived += proc_DataReceived;

					//proc.Start();

					//proc.BeginErrorReadLine();
					//proc.BeginOutputReadLine();

					//proc.WaitForExit();
					

					proc.Start();

					proc.BeginErrorReadLine();
					proc.BeginOutputReadLine();

					proc.WaitForExit();
					pipe.Stop();
		 */
		#endregion

		private static InstrumentCommandResult ExecuteInstrumentationCommand(InstrumentCommand instrumentCmd) {

			var retVal = new InstrumentCommandResult();

			retVal.Factory = new InstrumentFactory();

			var wildcards = instrumentCmd.Excludes.Select(pattern => new Wildcard(pattern)).ToList();
			var keyFilePath = !instrumentCmd.HasGlobalKeyFile
				? null
				: (instrumentCmd.GlobalKeyFile.IsAbsolute
					? instrumentCmd.GlobalKeyFile
					: instrumentCmd.GlobalKeyFile.ToAbsolutePath(instrumentCmd.BaseDirectory));

			if (keyFilePath != null && !IsValidStrongNameKeyFile(keyFilePath)) {
				//TODO message needs to list the assemblies that will not be instrumented
				// since each InstrumentCommand can have a different strong name key file
				ConsoleStub.WriteLine();
				ConsoleStub.WriteLine("The provided strong-name keyfile is not valid.");
				ConsoleStub.WriteLine("No assemblies will be instrumented.");
				return retVal;
			}

			// instrument all of the assemblies
			foreach (var originalAssemblyPath in instrumentCmd.AssemblyPaths) {
				ConsoleStub.WriteLine();

				var assembly = originalAssemblyPath.IsAbsolute
					? originalAssemblyPath
					: originalAssemblyPath.ToAbsolutePath(instrumentCmd.BaseDirectory);
				var assemblySymbols = assembly.ChangeExtension(".pdb");

				if (!FileStub.Exists(assembly.FullPath)) {
					ConsoleStub.WriteLine("Unable to find assembly '{0}'", assembly);
					ConsoleStub.WriteLine("It will not be instrumented.");
					continue;
				}

				// check if the symbols exist and ignore the current assembly if they don't
				// but only if symbols are required (symbols are required by default)
				var symbolsExist = FileStub.Exists(assemblySymbols.FullPath);
				if (!symbolsExist && !instrumentCmd.InstrumentWithoutSymbols) {
					ConsoleStub.Write("Unable to find a symbol (*.pdb) file for assembly '{0}'. ", assembly.FileName);
					ConsoleStub.Write("The assembly will not be instrumented. To instrument assemblies w/o a symbol file, use the 'withoutsymbols' argument. ");
					ConsoleStub.Write("(Symbol files are searched for in the assembly's directory. The symbol file was not found in '{0}')", assembly.ParentDirectory);
					ConsoleStub.WriteLine();
					continue;
				}

				if (!symbolsExist) {
					ConsoleStub.Write("Unable to find a symbol (*.pdb) file for assembly '{0}'. ", assembly.FileName);
					ConsoleStub.Write("The assembly will be instrumented, but will not contain source file information. ");
					ConsoleStub.Write("(Symbol files are searched for in the assembly's directory. The symbol file was not found in '{0}')", assembly.ParentDirectory);
					ConsoleStub.WriteLine();
				}

				if (wildcards.Any(wildcard => wildcard.IsMatch(assembly.FileNameWithoutExtension))) {
					continue;
				}

				if (IsFileStrongNameSigned(assembly) && !instrumentCmd.HasGlobalKeyFile) {
					ConsoleStub.Write("Assembly '{0}' is strong-name signed, but no strong-name keyfile ", assembly.FileName);
					ConsoleStub.Write("was provided to re-sign it. The assembly will not be instrumented. ");
					ConsoleStub.Write("To instrument strong-named assemblies, use the 'keyfile' argument to ");
					ConsoleStub.Write("specify the strong-name keyfile that will be used to resign the assembly ");
					ConsoleStub.Write("after instrumentation.");
					ConsoleStub.WriteLine();
					continue;
				}

				// backup the assembly and the pdb file
				var backupDir = instrumentCmd.GetBackupDirForAssembly(assembly);

				if (!DirectoryStub.Exists(backupDir.FullPath)) {
					DirectoryStub.CreateDirectory(backupDir.FullPath);
				}

				//2/19/11 Change by Joe Feser
				//If we have a file in the backup folder, we don't want to copy it again.
				//We need to come up with a way to always instrument the raw assembly each time if asked because if excludes

				var alreadyInstrumented = ILInstrumenter.IsAssemblyInstrumented(assembly, symbolsExist ? assemblySymbols : null);

				if (alreadyInstrumented) {
					retVal.AssembliesAlreadyInstrumented = true;
				}

				var copyPath = assembly.ChangeParentDirectory(backupDir);

				if (!File.Exists(copyPath) || !alreadyInstrumented) {
					FileStub.Copy(assembly, copyPath, true);
					if (symbolsExist) {
						FileStub.Copy(assemblySymbols, assemblySymbols.ChangeParentDirectory(backupDir), true);
					}
				}

				ILInstrumenter.InstrumentSequencePoints = instrumentCmd.InstrumentPoints;
				ILInstrumenter.InstrumentAssembly(assembly, symbolsExist ? assemblySymbols : null, keyFilePath, (IInstrumentFactory)retVal.Factory);

				if (alreadyInstrumented) {
					ConsoleStub.WriteLine("Assembly '{0}' was instrumented in a prior execution.", assembly.FileName);
				}
				else {
					ConsoleStub.WriteLine("Assembly '{0}' has been instrumented.", assembly.FileName);
				}

				ConsoleStub.WriteLine("Backups of the assembly {0}are in{1}    '{2}'",
					symbolsExist ? "and its symbols " : "",
					EnvironmentStub.NewLine,
					backupDir);
				ConsoleStub.WriteLine("To undo the instrumentation, use the 'restore' command.");
			}

			return retVal;
		}

		private static bool IsValidStrongNameKeyFile(FilePath file) {
			Contract.Requires<ArgumentNullException>(file != null, "file");
			Contract.Requires<ArgumentException>(file.IsAbsolute);

			try {
				using (var fs = FileStub.Open(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					var key = new System.Reflection.StrongNameKeyPair(fs);
				}
				return true;
			}
			catch (Exception) {
				return false;
			}
		}

		private static bool IsFileStrongNameSigned(FilePath file) {
			Contract.Requires<ArgumentNullException>(file != null, "file");
			Contract.Requires<ArgumentException>(file.IsAbsolute);

			var assemDef = Mono.Cecil.AssemblyDefinition.ReadAssembly(file.FullPath);
			return assemDef.Name.HasPublicKey;
		}

		private class InstrumentCommandResult {

			public bool AssembliesAlreadyInstrumented {
				get;
				set;
			}

			public IInstrumentationData Factory {
				get;
				set;
			}

		}
	}
}
