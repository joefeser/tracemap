package com.example.orders;

import java.math.BigDecimal;
import java.sql.Connection;

public class OrderService {
    private final OrderRepository repository = new OrderRepository();

    public BigDecimal calculateTotal(OrderResponse response, BigDecimal unitPrice) {
        if (response.status().equals("cancelled")) {
            return BigDecimal.ZERO;
        }

        repository.save(response.status());
        return unitPrice.multiply(BigDecimal.valueOf(response.itemCount()));
    }

    public void load(Connection connection, String status) throws Exception {
        connection.prepareStatement("select id, status from orders where status = ?");
        repository.save(status);
    }
}
