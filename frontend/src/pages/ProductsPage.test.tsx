import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ordersApi } from "../api/orders";
import { productsApi } from "../api/products";
import { ProductsPage } from "./ProductsPage";

vi.mock("../api/products");
vi.mock("../api/orders");

const products = [
  { id: 1, name: "Laptop", price: 1200, stock: 10, isActive: true },
  { id: 2, name: "Mouse", price: 50, stock: 0, isActive: true },
];

function renderPage() {
  return render(
    <MemoryRouter>
      <ProductsPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.mocked(productsApi.getAll).mockResolvedValue(products);
});

it("renders each product and disables Buy for out-of-stock items", async () => {
  renderPage();

  expect(await screen.findByText("Laptop")).toBeInTheDocument();
  expect(screen.getByText("Mouse")).toBeInTheDocument();

  const buyButtons = screen.getAllByRole("button", { name: "Buy" });
  expect(buyButtons[0]).toBeEnabled(); // Laptop, in stock
  expect(buyButtons[1]).toBeDisabled(); // Mouse, out of stock
});

it("rejects a quantity above available stock without calling the API", async () => {
  const user = userEvent.setup();
  renderPage();

  const [laptopBuyButton] = await screen.findAllByRole("button", { name: "Buy" });
  await user.click(laptopBuyButton);

  const quantityInput = screen.getByLabelText("Quantity for Laptop");
  await user.clear(quantityInput);
  await user.type(quantityInput, "99");
  await user.click(screen.getByRole("button", { name: "Confirm" }));

  await waitFor(() => expect(screen.getByText("Only 10 in stock.")).toBeInTheDocument());
  expect(ordersApi.create).not.toHaveBeenCalled();
});
