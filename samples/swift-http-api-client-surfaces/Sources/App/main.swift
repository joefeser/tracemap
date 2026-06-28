import Foundation

func foundationRequests() {
    var postRequest = URLRequest(url: URL(string: "https://api.example.invalid/v1/users/123/roles?token=super-secret")!)
    postRequest.httpMethod = "POST"
    URLSession.shared.dataTask(with: postRequest)

    var deleteRequest = URLRequest(url: URL(string: "https://api.example.invalid/v1/sessions/abcdef123456")!)
    deleteRequest.httpMethod = "DELETE"
    URLSession.shared.dataTask(with: deleteRequest)

    var unknownMethod = URLRequest(url: URL(string: "https://api.example.invalid/v1/unknown")!)
    URLSession.shared.dataTask(with: unknownMethod)

    let dynamicURL = URL(string: "https://api.example.invalid/v1/" + "dynamic")!
    var dynamicRequest = URLRequest(url: dynamicURL)
    dynamicRequest.httpMethod = "GET"
    URLSession.shared.dataTask(with: dynamicRequest)
}

func alamofireLike(endpoint: String) {
    AF.request("https://api.example.invalid/v1/orders/42", method: .get)
    Alamofire.request("https://api.example.invalid/v1/orders/43?api_key=do-not-render", method: .post)
    AF.request(endpoint, method: .put)
}

protocol TargetType {}

enum UserAPI: TargetType {
    var baseURL: URL { URL(string: "https://api.example.invalid")! }
    var path: String { "/v1/users/123/roles" }
    var method: Moya.Method { .get }
}

enum UnknownMethodAPI: TargetType {
    var baseURL: URL { URL(string: "https://api.example.invalid")! }
    var path: String { "/v1/missing-method" }
}

enum Moya {
    enum Method {
        case get
        case post
    }
}

enum AF {
    static func request(_ value: String, method: Method) {}
    enum Method {
        case get
        case put
    }
}

enum Alamofire {
    static func request(_ value: String, method: Method) {}
    enum Method {
        case post
    }
}

