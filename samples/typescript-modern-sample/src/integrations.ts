declare const z: {
  object(value: unknown): unknown;
  string(): unknown;
};

declare const prisma: {
  customer: {
    findMany(args?: unknown): Promise<unknown[]>;
  };
};

declare const base44: {
  entities: {
    Customer: {
      filter(where: unknown, orderBy?: string): Promise<unknown[]>;
      create(data: unknown): Promise<unknown>;
    };
  };
};

declare const app: {
  get(route: string, handler: unknown): void;
};

export const CustomerSchema = z.object({
  status: z.string()
});

export async function callBilling(status: string): Promise<void> {
  await fetch("https://billing.example/status", { method: "POST", body: JSON.stringify({ status }) });
}

export async function loadCustomers(): Promise<unknown[]> {
  return prisma.customer.findMany({
    where: { status: "active", organization_id: "org_1" },
    orderBy: { updated_at: "desc" },
    select: { status: true, total: true }
  });
}

export async function loadBase44Customers(): Promise<unknown[]> {
  return base44.entities.Customer.filter({ status: "active", organization_id: "org_1" }, "-updated_at");
}

export async function createBase44Customer(status: string, total: number): Promise<unknown> {
  return base44.entities.Customer.create({ status, total });
}

app.get("/customers/:id", (_request: unknown) => {
  const endpoint = process.env.CUSTOMER_ENDPOINT;
  return JSON.parse(JSON.stringify({ endpoint }));
});
