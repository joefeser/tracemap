#if os(Linux)
import Glibc
#else
import Darwin
#endif
import TraceMapSwift

exit(Int32(TraceMapSwiftCLI.run(arguments: Array(CommandLine.arguments.dropFirst()))))
