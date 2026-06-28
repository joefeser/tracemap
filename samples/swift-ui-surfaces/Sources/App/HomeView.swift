import SwiftUI

struct HomeView: View {
    @State private var showingDetails = false

    var body: some View {
        NavigationStack {
            List {
                NavigationLink(destination: DetailView()) {
                    Text("Details")
                }
                Button("Refresh") {
                    refresh()
                }
            }
            .sheet(isPresented: $showingDetails) {
                DetailView()
            }
            .alert("Saved", isPresented: $showingDetails) {
                Button("OK", role: .cancel) {}
            }
            .toolbar {
                ToolbarItem(placement: .primaryAction) {
                    Button("Save") {
                        save()
                    }
                }
            }
            .onAppear {
                refresh()
            }
        }
    }

    func refresh() {}
    func save() {}
}

struct DetailView: View {
    var body: some View {
        Text("Detail")
    }
}

struct NotActuallyAView {
    let title: String
}
