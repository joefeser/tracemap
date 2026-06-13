def calculate_total(order) -> float:
    subtotal = order.total
    tax = subtotal * 0.08
    return subtotal + tax
