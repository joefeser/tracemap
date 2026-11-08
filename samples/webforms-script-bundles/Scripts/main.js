function saveRecord() {
    return fetch("/api/Save.ashx", { method: "POST" });
}
