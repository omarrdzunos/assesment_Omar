import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiError } from "../api/client";
import { ordersApi } from "../api/orders";
import { useApi } from "../hooks/useApi";

export function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const orderId = Number(id);
  const { data: order, loading, error, refetch } = useApi(() => ordersApi.getById(orderId), [orderId]);
  const [cancelError, setCancelError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  async function handleCancel() {
    setCancelling(true);
    setCancelError(null);
    try {
      await ordersApi.cancel(orderId);
      await refetch();
    } catch (err) {
      setCancelError(err instanceof ApiError ? err.message : "Could not cancel the order.");
    } finally {
      setCancelling(false);
    }
  }

  if (loading) {
    return <p className="state-message">Loading order…</p>;
  }

  if (error) {
    return (
      <div className="state-message state-error">
        <p>{error}</p>
        <button onClick={refetch}>Retry</button>
      </div>
    );
  }

  if (!order) {
    return null;
  }

  return (
    <section>
      <Link to="/orders">&larr; Back to orders</Link>
      <h2>Order #{order.id}</h2>
      <p>Date: {new Date(order.createdAt).toLocaleString()}</p>
      <p>
        Status: <span className={`status-badge status-${order.status.toLowerCase()}`}>{order.status}</span>
      </p>

      <div className="table-wrapper">
        <table className="data-table">
          <thead>
            <tr>
              <th>Product</th>
              <th>Quantity</th>
              <th>Unit Price</th>
              <th>Subtotal</th>
            </tr>
          </thead>
          <tbody>
            {order.items.map((item) => (
              <tr key={item.productId}>
                <td data-label="Product">{item.productName}</td>
                <td data-label="Quantity">{item.quantity}</td>
                <td data-label="Unit Price">${item.unitPrice.toFixed(2)}</td>
                <td data-label="Subtotal">${(item.quantity * item.unitPrice).toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="order-total">Total: ${order.total.toFixed(2)}</p>

      {order.status === "Pending" && (
        <button onClick={handleCancel} disabled={cancelling}>
          {cancelling ? "Cancelling…" : "Cancel order"}
        </button>
      )}
      {cancelError && <p className="field-error">{cancelError}</p>}
    </section>
  );
}
