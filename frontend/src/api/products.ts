import { api } from "./client";
import type { Product } from "./types";

export const productsApi = {
  getAll: () => api.get<Product[]>("/api/products"),
};
