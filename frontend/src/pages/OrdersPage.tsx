import { Link } from "react-router-dom";
import { ordersApi } from "../api/orders";
import { useApi } from "../hooks/useApi";

export function OrdersPage() {
  const { data: orders, loading, error, refetch } = useApi(ordersApi.getAll);

  if (loading) {
    return <p className="state-message">Loading orders…</p>;
  }

  if (error) {
    return (
      <div className="state-message state-error">
        <p>{error}</p>
        <button onClick={refetch}>Retry</button>
      </div>
    );
  }

  if (!orders || orders.length === 0) {
    return <p className="state-message">No orders yet.</p>;
  }

  return (
    <section>
      <h2>Orders</h2>
      <div className="table-wrapper">
        <table className="data-table">
          <thead>
            <tr>
              <th>Order #</th>
              <th>Date</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order) => (
              <tr key={order.id}>
                <td data-label="Order #">
                  <Link to={`/orders/${order.id}`}>{order.id}</Link>
                </td>
                <td data-label="Date">{new Date(order.createdAt).toLocaleDateString()}</td>
                <td data-label="Status">
                  <span className={`status-badge status-${order.status.toLowerCase()}`}>{order.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
