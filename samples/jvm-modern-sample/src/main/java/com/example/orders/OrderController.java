package com.example.orders;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/orders")
public class OrderController {
    private final OrderService service = new OrderService();

    @GetMapping("/{id}")
    public OrderResponse getOrder(String id) {
        OrderResponse response = new OrderResponse();
        response.setStatus(id);
        service.calculateTotal(response, java.math.BigDecimal.ONE);
        return response;
    }
}
