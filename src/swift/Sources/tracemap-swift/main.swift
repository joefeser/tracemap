import Darwin
import TraceMapSwift

exit(Int32(TraceMapSwiftCLI.run(arguments: Array(CommandLine.arguments.dropFirst()))))
