import { mockCustomerBranches } from "../data/branches.mock";
import type { CustomerBranch } from "../types";

export async function getMockCustomerBranches(): Promise<CustomerBranch[]> {
  return mockCustomerBranches.map((branch) => ({ ...branch }));
}
