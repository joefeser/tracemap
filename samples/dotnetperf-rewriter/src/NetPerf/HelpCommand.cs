using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetPerf {
	[System.Diagnostics.DebuggerDisplay("{_forCommand} {_forCommandArgument}")]
	internal class HelpCommand {

		private readonly string _forCommand;
		private readonly string _forCommandArgument;

		readonly Dictionary<string, Action> _helpTopics;

		public static string[] KnownCommands = new[]{
			"help",
			"instrument",
			"restore",
			"points", //TODO for not free version add help for points.
			"useconfig"
		};

		private static string[] ExtraCommands = new[] { 
			"task",
			"tasks",
			"wildcards"
		};

		public HelpCommand(string command = null, string commandArgument = null) {
			_forCommand = command;
			_forCommandArgument = commandArgument;

			_helpTopics = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase) {
				{"instrument", HelpForInstrument },
				{"restore", HelpForRestore },
				{"help", HelpForHelp },
				{"useconfig", HelpForUseConfig},
				
				{"task", HelpForTask },
				{"tasks", HelpForTasks },
				{"wildcards", HelpForWildcards },
			};
		}

		public static void DisplayBasicHelp() {
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("basic commands:");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  help          ask for help on a topic or a command");
			ConsoleStub.WriteLine("  instrument    create instrumented assemblies for profiling later");
			ConsoleStub.WriteLine("  restore       restore assembly and symbol files backed up by 'instrument'");
			ConsoleStub.WriteLine("  useconfig     use a configuration file created by the Netperf GUI");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("For additional help on a particular command, request it using the help command");
			ConsoleStub.WriteLine("e.g. netperf help [command name]");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("additional help topics:");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  tasks         help getting started");
			ConsoleStub.WriteLine("  wildcards     description of wildcard patterns, and how netperf uses them");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("For additional help on a particular topic, request it using the help command");
			ConsoleStub.WriteLine("e.g. netperf help [topic name]");
			ConsoleStub.WriteLine();
		}

		public void DisplayHelp() {
			if (_forCommand.IsNullOrWhitespace() && _forCommandArgument.IsNullOrWhitespace()) {
				DisplayBasicHelp();
				return;
			}

			if (!_helpTopics.ContainsKey(_forCommand)) {
				ConsoleStub.WriteLine();
				ConsoleStub.WriteLine("Unrecognized help topic '{0}'", _forCommand);
				ConsoleStub.WriteLine();
				DisplayBasicHelp();
				return;
			}

			ConsoleStub.WriteLine();
			_helpTopics[_forCommand]();
			ConsoleStub.WriteLine();
		}

		void HelpForInstrument() {
			ConsoleStub.WriteLine("netperf instrument ASSEMBLIES [backupdir DIRECTORY] [exclude WILDCARD]");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("instruments the specified ASSEMBLIES");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  attempts to instrument each ASSEMBLY file to gather various code metrics");
			ConsoleStub.WriteLine("  when the assembly is executed within a process.");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  ASSEMBLIES are copied into the backupdir before being instrumented");
			CommonOptionsForInstrumentAndRestore();
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  withoutsymbols");
			ConsoleStub.WriteLine("    forces the instrumentation to continue even when no symbol (*.pdb)");
			ConsoleStub.WriteLine("    file is found for an assembly. When this option is present, assemblies");
			ConsoleStub.WriteLine("    with symbol files will be instrumented as normal, but assemblies without");
			ConsoleStub.WriteLine("    symbol files will not be instrumented with source file information.");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  keyfile FILE PATH");
			ConsoleStub.WriteLine("    provide a strong-name keyfile that will be used to resign any assemblies");
			ConsoleStub.WriteLine("    that are strong-name signed. If an assembly is strong-name signed and");
			ConsoleStub.WriteLine("    no keyfile is provided, the assembly will not be instrumented.");
		}

		void CommonOptionsForInstrumentAndRestore() {
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("options:");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  backupdir DIRECTORY");
			ConsoleStub.WriteLine("    sets the backup directory for the command, which is where all of the");
			ConsoleStub.WriteLine("    ASSEMBLIES are stored prior to instrumentation. If this option is not");
			ConsoleStub.WriteLine("    given, the default backupdir (for each assembly) is the directory of");
			ConsoleStub.WriteLine("    the assembly with 'backup' appended.");
			ConsoleStub.WriteLine("    e.g. if the assembly is \"hello\\assembly1.dll\" then the default");
			ConsoleStub.WriteLine("    backupdir for the assembly is \"hello\\backup\\\"");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  exclude WILDCARD");
			ConsoleStub.WriteLine("    assemblies whose names match the WILDCARD will not be processed");
			ConsoleStub.WriteLine("    this option can be used multiple times");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("    use 'netperf help wildcards' for information on WILDCARD syntax");
		}

		void HelpForRestore() {
			ConsoleStub.WriteLine("netperf restore ASSEMBLIES [backupdir DIRECTORY] [exclude WILDCARD]");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("reverses the effects of the 'instrument' command");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  attempts to de-instrument the ASSEMBLY files by copying the backups that");
			ConsoleStub.WriteLine("  the 'instrument' command created back to their original locations.");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  to reliably undo the effects of the 'instrument' command, the 'restore'");
			ConsoleStub.WriteLine("  arguments should be a mirror image of those given to 'instrument'");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  for example, if the 'instrument' comand was");
			ConsoleStub.WriteLine(@"    netperf instrument myassembly1.dll bin\debug\hello.exe");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  then the 'restore' command to undo it would be");
			ConsoleStub.WriteLine(@"    netperf restore myassembly1.dll bin\debug\hello.exe");
			CommonOptionsForInstrumentAndRestore();
		}

		void HelpForHelp() {
			ConsoleStub.WriteLine("netperf help [COMMAND] [COMMAND-ARG (optional)]");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  [COMMAND] is one of");
			ConsoleStub.WriteLine("    help");
			ConsoleStub.WriteLine("    instrument");
			ConsoleStub.WriteLine("    restore");
			ConsoleStub.WriteLine("    task");
			ConsoleStub.WriteLine("    tasks");
			ConsoleStub.WriteLine("    wildcards");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  [COMMAND-ARG] is defined by each [COMMAND]");
			ConsoleStub.WriteLine("    for the 'task' command: ");
			ConsoleStub.WriteLine("      [COMMAND-ARG] is the task name to ask for help on.");
			ConsoleStub.WriteLine("      use 'netperf help tasks' to see the list of available task names");
		}

		void HelpForUseConfig() {
			ConsoleStub.WriteLine("netperf useconfig FILEPATH [basedir DIRECTORY]");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("executes the commands in FILEPATH configuration file");
			ConsoleStub.WriteLine("NO OTHER COMMANDS MAY BE PRESENT WHEN THIS ONE IS USED");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  The FILEPATH configuration file is created by the netperf UI and should");
			ConsoleStub.WriteLine("  be changed at your own risk.");
			ConsoleStub.WriteLine();
			ConsoleStub.WriteLine("  Typically, such a file will contain commands to instrument several assemblies,");
			ConsoleStub.WriteLine("  run an executable (that presumably uses those assemblies), and then create");
			ConsoleStub.WriteLine("  one or more reports from the gathered data.");
		}

		void HelpForTask() {
			//show help for a particular task
			var task = _forCommandArgument;
			ConsoleStub.WriteLine("Help for particular tasks is coming soon");
		}

		void HelpForTasks() {
			// show a list of the available tasks
			ConsoleStub.WriteLine("Help for 'tasks' is coming soon!");
		}

		void HelpForWildcards() {
			ConsoleStub.WriteLine("Help for 'wildcards' is coming soon!");
		}

	}
}
