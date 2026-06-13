package com.example.routes

data class RunnerStatus(val status: String)

fun registerRoutes() {
    get("/api/runners/{id}") {
        val status = RunnerStatus("ready")
        println(status.status)
    }
}
