interface CustomerContract {
  status: string;
  total: number;
}

class BaseHandler {
  handle(customer: CustomerContract): string {
    return customer.status;
  }
}

export class CustomerHandler extends BaseHandler implements CustomerContract {
  status = "active";
  total = 42;

  override handle(customer: CustomerContract): string {
    const alias = customer;
    const adjusted = alias.total + 10;
    return `${alias.status}:${adjusted}`;
  }
}

export function sendCustomer(customer: CustomerContract): string {
  const handler = new CustomerHandler();
  return handler.handle(customer);
}
