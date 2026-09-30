export interface Product {
  id: number;
  name: string;
  price: number;
  stock: number;
  isActive: boolean;
}

export type OrderStatus = "Pending" | "Cancelled";

export interface OrderItem {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
}

export interface Order {
  id: number;
  createdAt: string;
  status: OrderStatus;
  items: OrderItem[];
  total: number;
}

export interface OrderSummary {
  id: number;
  createdAt: string;
  status: OrderStatus;
}

export interface CreateOrderItemRequest {
  productId: number;
  quantity: number;
}

export interface CreateOrderRequest {
  items: CreateOrderItemRequest[];
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
}
