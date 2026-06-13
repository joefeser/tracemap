package com.example.broken

class BrokenRoutes(

fun register() {
    get("/broken") { println("broken") }
}
