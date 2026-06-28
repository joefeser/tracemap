import Foundation
import Security
import SQLite3
import RealmSwift
import GRDB

enum PreferenceKeys {
    static let launchCount = "launchCount"
    static let sensitivePreference = "authToken"
}

final class PreferencesStore {
    func update(flag: Bool, dynamicKey: String) {
        UserDefaults.standard.register(defaults: ["welcomeMessage": "hello", "syncEnabled": true])
        UserDefaults.standard.set(flag, forKey: "hasCompletedOnboarding")
        _ = UserDefaults.standard.integer(forKey: PreferenceKeys.launchCount)
        _ = UserDefaults.standard.string(forKey: PreferenceKeys.sensitivePreference)
        UserDefaults.standard.removeObject(forKey: dynamicKey)
    }

    func firstScopedAlias() {
        let key = "firstScopedPreference"
        _ = UserDefaults.standard.string(forKey: key)
    }

    func secondScopedAlias() {
        let key = "secondScopedPreference"
        _ = UserDefaults.standard.string(forKey: key)
    }
}

final class CredentialStore {
    func save() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: "payments-service",
            kSecAttrAccount as String: "primary-user@example.invalid",
            kSecValueData as String: Data()
        ]
        SecItemAdd(query as CFDictionary, nil)
    }

    func load(configQuery: CFDictionary) {
        SecItemCopyMatching(configQuery, nil)
    }
}

final class SQLStore {
    func load(db: Database, fmdb: FMDatabase, rawDb: OpaquePointer?, name: String) throws {
        try db.execute(sql: "CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY, email TEXT)")
        _ = try Row.fetchAll(db, sql: "SELECT id, email FROM users WHERE email = ?")
        try db.execute(sql: "UPDATE orders SET status = 'shipped', total = 10 WHERE id = 1;")
        fmdb.executeUpdate("INSERT INTO audit_events (name) VALUES (?)")
        sqlite3_prepare_v2(rawDb, "DELETE FROM sessions WHERE expires_at < ?", -1, nil, nil)
        try db.execute(sql: "SELECT * FROM " + name)
    }
}

final class AccountObject: Object {
    @Persisted(primaryKey: true) var id: String
    @Persisted var email: String

    override static func primaryKey() -> String? { "id" }
}

final class RealmStore {
    func query(realm: Realm) {
        _ = realm.objects(AccountObject.self).filter("email == %@", "private@example.invalid")
    }
}

protocol Database {
    func execute(sql: String) throws
}

enum Row {
    static func fetchAll(_ db: Database, sql: String) throws -> [String] { [] }
}

final class FMDatabase {
    func executeUpdate(_ sql: String) {}
}
