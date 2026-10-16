using System;
using System.Collections.Generic;
using System.Linq;
using NetPerf.Common;
using NetPerf.Common.Configuration;

namespace NetPerf {

	internal static class NetPerfCommandLineParser {

		public static ErrorTuple<List<object>> Parse(params string[] commandLineArgs) {
			if (commandLineArgs.IsEmpty()) {
				return ErrorTuple.Create(new List<object> { new HelpCommand() });
			}

			var arguments = new CommandLineParser(commandLineArgs);

			var commands = new List<object>();
			var commandsFromConfig = new List<object>();

			while (arguments.MoveNext()) {
				var cur = arguments.Current;

				switch (cur.AsKeyValue()) {
					case "help":
						commands.Add(ParseHelpCommand(arguments));
						break;
					case "instrument":
						var error = ParseInstrumentCommand(arguments);
						if (error.HasError) {
							return new ErrorTuple<List<object>>(error.ErrorMessage);
						}
						commands.Add(error.Data);
						break;
					case "restore":
						var err = ParseRestoreCommand(arguments);
						if (err.HasError) {
							return new ErrorTuple<List<object>>(err.ErrorMessage);
						}
						commands.Add(err.Data);
						break;
					case "pauseonerror":
						commands.Add(ParsePauseOnErrorCommand(arguments));
						break;
					case "points":
						commands.Add(ParsePointsCommand(arguments));
						break;
					case "useconfig":
						var result = ParseUseConfigCommand(arguments);
						if (result.HasError) {
							return new ErrorTuple<List<object>>(result.ErrorMessage);
						}
						commandsFromConfig.AddRange(result.Data);
						break;
					case "localreferences":
						var listRefs = ParseListLocalReferencesCommand(arguments);
						if (listRefs.HasError) {
							return new ErrorTuple<List<object>>(listRefs.ErrorMessage);
						}
						commands.Add(listRefs.Data);
						break;
					default:
						return new ErrorTuple<List<object>>("Unrecognized command: '{0}'. Unable to parse command line.{1}Type 'netperf help' for more help", cur.Value, EnvironmentStub.NewLine);
				}
			}

			//4/14/2011 make sure we allow pauseonerror
			if (commandsFromConfig.IsNotEmpty() &&
				commands.Where(item => item.GetType() != typeof(PointsCommand) && item.GetType() != typeof(PauseOnErrorCommand)).IsNotEmpty()) {
				return new ErrorTuple<List<object>>("The useconfig command cannot be used with any other commands."
					+ EnvironmentStub.NewLine
					+ "Type 'netperf help useconfig' for more help.");
			}

			commands.AddRange(commandsFromConfig);

			return ErrorTuple.Create(commands);
		}

		private static ErrorTuple<List<object>> ParseUseConfigCommand(CommandLineParser parser) {

			FilePath configPath = null;
			DirectoryPath baseDir = null;

			if (parser.MoveNext()) {
				configPath = FilePath.Parse(parser.Current);
			}
			else {
				return new ErrorTuple<List<object>>("The useconfig command requires a FILEPATH."
					+ EnvironmentStub.NewLine
					+ "Type 'netperf help useconfig' for more help.");
			}

			if (parser.MoveNext()) {
				if (!"basedir".IgnoreCaseEq(parser.Current)) {
					parser.MovePrevious();
				}
				else {
					if (!parser.MoveNext()) {
						return new ErrorTuple<List<object>>("The 'basedir' argument requires a DIRECTORY path."
							+ EnvironmentStub.NewLine
							+ "Type 'netperf help useconfig' for more help.");
					}
					else {
						baseDir = DirectoryPath.Parse(parser.Current);
					}
				}
			}

			var result = InstrumentationSettingsParser.Read(configPath);
			if (result.HasError) {
				return new ErrorTuple<List<object>>("Could not read the configuration file at '" + configPath + "'"
					+ EnvironmentStub.NewLine
					+ result.ErrorMessage);
			}

			var config = result.Data;
			var list = new List<object>();
			if (config.AssembliesToInstrument.IsNotEmpty()) {
				var instrument = new InstrumentCommand();
				instrument.AssemblyPaths.AddRange(config.AssembliesToInstrument.Distinct());
				instrument.GlobalKeyFile = config.StrongNameKeyFile;
				instrument.InstrumentWithoutSymbols = true;

				if (baseDir != null) {
					instrument.SetBaseDirectory(baseDir);
				}

				list.Add(instrument);
			}

			if (config.ApplicationToRun != null) {
				list.Add(new RunExternalProcessCommand {
					ApplicationBasePath = baseDir,
					ApplicationParameters = config.ApplicationParameters,
					ApplicationToRun = config.ApplicationToRun
				});
			}

			if (config.ExtractReport != null && config.ExtractReport.IsActive) {
				list.Add(new CreateReportCommand {
					OutputFile = config.ExtractReport.OutputFile,
					TypeOfReport = config.ExtractReport.OutputType
				});
			}

			if (config.SummaryReport != null && config.SummaryReport.IsActive) {
				list.Add(new CreateReportCommand {
					OutputFile = config.SummaryReport.OutputFile,
					TypeOfReport = config.SummaryReport.OutputType
				});
			}

			return ErrorTuple.Create(list);
		}

		private static ErrorTuple<RestoreCommand> ParseRestoreCommand(CommandLineParser parser) {
			var intermediate = ParseInstrumentCommand(parser);
			if (intermediate.HasError) {
				return new ErrorTuple<RestoreCommand>(intermediate.ErrorMessage);
			}

			return ErrorTuple.Create(new RestoreCommand(intermediate.Data));
		}

		private static HelpCommand ParseHelpCommand(CommandLineParser parser) {
			string command = null;
			string commandArg = null;

			if (parser.MoveNext())
				command = parser.Current.Value;

			if (parser.MoveNext())
				commandArg = parser.Current.Value;

			return new HelpCommand(command, commandArg);
		}

		private static PointsCommand ParsePointsCommand(CommandLineParser parser) {
			string command = null;

			if (parser.MoveNext())
				command = parser.Current.Value;

			return new PointsCommand() {
				InstrumentPoints = true
			};
		}

		private static PauseOnErrorCommand ParsePauseOnErrorCommand(CommandLineParser parser) {
			string command = null;

			if (parser.MoveNext())
				command = parser.Current.Value;

			return new PauseOnErrorCommand() {
				Pause = true
			};
		}

		private static ErrorTuple<InstrumentCommand> ParseInstrumentCommand(CommandLineParser parser) {

			var command = new InstrumentCommand();

			while (parser.MoveNext()) {
				var cur = parser.Current;
				var value = cur.AsKeyValue();

				if (HelpCommand.KnownCommands.Any(cmd => cmd.IgnoreCaseEq(value))) {
					parser.MovePrevious();
					break;
				}

				switch (value) {
					case "backupdir":
						if (parser.MoveNext().Current.IsKey) {
							return new ErrorTuple<InstrumentCommand>("Invalid command line. The 'backupDir' argument is missing its parameter.");
						}

						command.BackupDirectory = DirectoryPath.Parse(parser.Current);
						break;

					case "exclude":
						if (parser.MoveNext().Current.IsKey) {
							return new ErrorTuple<InstrumentCommand>("Invalid command line. One of the 'exclude' arguments is missing its parameter.");
						}

						command.Excludes.Add(parser.Current);
						break;
					case "withoutsymbols":
					case "force":
						command.InstrumentWithoutSymbols = true;
						break;
					case "keyfile":
						if (parser.MoveNext().Current.IsKey) {
							return new ErrorTuple<InstrumentCommand>("Invalid command line. The 'keyfile' argument is missing its parameter.");
						}

						command.GlobalKeyFile = FilePath.Parse(parser.Current);
						break;
					default:
						// if nothing else works, assume that the argument is an assembly path
						var path = FilePath.Parse(cur);
						command.AssemblyPaths.Add(path);
						break;
				}
			}

			// on the command line, the base directory is the environment's current directory
			// the base directory must be set so that any relative paths will be converted to
			// absolute paths to make them easier to use
			// this needs to stay the directory that the application is being executed from and not the netperf folder.
			command.SetBaseDirectory(DirectoryPath.CurrentDirectory);
			return new ErrorTuple<InstrumentCommand>(command);
		}

		private static ErrorTuple<ListLocalReferencesCommand> ParseListLocalReferencesCommand(CommandLineParser parser) {
			var command = new ListLocalReferencesCommand();

			while (parser.MoveNext()) {
				var cur = parser.Current;
				var value = cur.AsKeyValue();

				if (HelpCommand.KnownCommands.Any(cmd => cmd.IgnoreCaseEq(value))) {
					parser.MovePrevious();
					break;
				}

				command.RootAssembly = FilePath.Parse(value);
			}

			if (command.RootAssembly == null)
				return new ErrorTuple<ListLocalReferencesCommand>("Invalid command line. The 'localreferences' argument is missing its 'ROOT_ASSEMBLY' parameter.");

			return new ErrorTuple<ListLocalReferencesCommand>(command);
		}
	}

}
