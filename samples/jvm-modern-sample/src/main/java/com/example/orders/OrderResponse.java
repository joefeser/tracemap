package com.example.orders;

import com.fasterxml.jackson.annotation.JsonProperty;
import jakarta.persistence.Entity;

@Entity
public class OrderResponse {
    @JsonProperty("status")
    private String status;

    private int itemCount;

    public String status() {
        return status;
    }

    public void setStatus(String status) {
        this.status = status;
    }

    public int itemCount() {
        return itemCount;
    }
}
