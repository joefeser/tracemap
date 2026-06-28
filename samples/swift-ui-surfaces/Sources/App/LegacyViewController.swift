import UIKit

final class LegacyViewController: UIViewController {
    @IBOutlet weak var titleLabel: UILabel?

    override func viewDidLoad() {
        super.viewDidLoad()
        let button = UIButton(type: .system)
        button.addTarget(self, action: #selector(handleTap), for: .touchUpInside)
    }

    @IBAction func savePressed(_ sender: Any) {
        persist()
    }

    @objc func handleTap() {
        persist()
    }

    func persist() {}
}
