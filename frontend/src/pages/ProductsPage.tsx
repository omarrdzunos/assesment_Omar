import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { ApiError } from "../api/client";
import { productsApi } from "../api/products";
import { ordersApi } from "../api/orders";
import { useApi } from "../hooks/useApi";

export function ProductsPage() {
  const { data: products, loading, error, refetch } = useApi(productsApi.getAll);
  const [buyingId, setBuyingId] = useState<number | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const navigate = useNavigate();

  function startBuying(productId: number) {
    setBuyingId(productId);
    setQuantity(1);
    setFormError(null);
  }

  function cancelBuying() {
    setBuyingId(null);
    setFormError(null);
  }

  async function confirmBuy(productId: number, stock: number) {
    if (!Number.isInteger(quantity) || quantity <= 0) {
      setFormError("Quantity must be a whole number greater than zero.");
      return;
    }
    if (quantity > stock) {
      setFormError(`Only ${stock} in stock.`);
      return;
    }

    setSubmitting(true);
    setFormError(null);
    try {
      const order = await ordersApi.create({ items: [{ productId, quantity }] });
      setBuyingId(null);
      navigate(`/orders/${order.id}`);
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Could not place the order.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <p className="state-message">Loading products…</p>;
  }

  if (error) {
    return (
      <div className="state-message state-error">
        <p>{error}</p>
        <button onClick={refetch}>Retry</button>
      </div>
    );
  }

  return (
    <section>
      <h2>Products</h2>
      <div className="table-wrapper">
        <table className="data-table">
          <thead>
            <tr>
              <th>Product</th>
              <th>Price</th>
              <th>Stock</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {products?.map((product) => (
              <tr key={product.id}>
                <td data-label="Product">{product.name}</td>
                <td data-label="Price">${product.price.toFixed(2)}</td>
                <td data-label="Stock">{product.stock}</td>
                <td data-label="Action">
                  {buyingId === product.id ? (
                    <div className="buy-form">
                      <input
                        type="number"
                        min={1}
                        max={product.stock}
                        value={quantity}
                        onChange={(e) => setQuantity(Number(e.target.value))}
                        aria-label={`Quantity for ${product.name}`}
                      />
                      <button onClick={() => confirmBuy(product.id, product.stock)} disabled={submitting}>
                        Confirm
                      </button>
                      <button onClick={cancelBuying} disabled={submitting}>
                        Cancel
                      </button>
                      {formError && <p className="field-error">{formError}</p>}
                    </div>
                  ) : (
                    <button onClick={() => startBuying(product.id)} disabled={!product.isActive || product.stock === 0}>
                      Buy
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
