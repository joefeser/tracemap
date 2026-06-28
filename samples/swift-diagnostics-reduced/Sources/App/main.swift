import Foundation

#if canImport(UIKit)
@objcMembers class LegacyBridge: NSObject {
    @IBAction func tapped(_ sender: Any) {
        perform(#selector(runDynamic))
        _ = NSClassFromString("RuntimeOnly")
        _ = Mirror(reflecting: self)
    }

    @objc func runDynamic() {}
}
#endif

#Preview {
    Text("Diagnostics")
}

protocol DiagnosticServing {
    func run()
}

extension DiagnosticServing {
    func run() {}
}
