declare const z: {
  object(value: unknown): unknown;
  string(): unknown;
};

declare const prisma: {
  customer: {
    findMany(): Promise<unknown[]>;
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
  return prisma.customer.findMany();
}

app.get("/customers/:id", (_request: unknown) => {
  const endpoint = process.env.CUSTOMER_ENDPOINT;
  return JSON.parse(JSON.stringify({ endpoint }));
});
