import { api } from "./client";
import type { CreateOrderRequest, Order, OrderSummary } from "./types";

export const ordersApi = {
  getAll: () => api.get<OrderSummary[]>("/api/orders"),
  getById: (id: number) => api.get<Order>(`/api/orders/${id}`),
  create: (request: CreateOrderRequest) => api.post<Order>("/api/orders", request),
  cancel: (id: number) => api.post<void>(`/api/orders/${id}/cancel`),
};
