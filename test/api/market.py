import requests

from api.base import BaseAPIClient


class MarketAPI(BaseAPIClient):
    PREFIX = "/api"

    def login(self, email: str, password: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/auth/login", json={
            "email": email,
            "password": password,
        })

    # ---- Wallet ----

    def get_wallet(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/wallet/me")

    def deposit(self, amount: float) -> requests.Response:
        return self.post(f"{self.PREFIX}/wallet/deposit", json={
            "amount": amount,
        })

    # ---- Products ----

    def get_products(self, status: str | None = None) -> requests.Response:
        params = {}
        if status is not None:
            params["status"] = status
        return self.get(f"{self.PREFIX}/products", params=params)

    def get_product(self, product_id: int) -> requests.Response:
        return self.get(f"{self.PREFIX}/products/{product_id}")

    def create_product(self, title: str, price: float, stock: int = 10,
                       description: str = "", image_urls: list[str] | None = None,
                       category: str = "其他", condition: str = "良好") -> requests.Response:
        return self.post(f"{self.PREFIX}/products", json={
            "title": title,
            "description": description,
            "price": price,
            "stock": stock,
            "category": category,
            "condition": condition,
            "imageUrls": image_urls or [],
        })

    def update_product(self, product_id: int, title: str, price: float, stock: int = 10,
                       description: str = "", status: str = "Active",
                       image_urls: list[str] | None = None,
                       category: str = "其他", condition: str = "良好") -> requests.Response:
        return self.put(f"{self.PREFIX}/products/{product_id}", json={
            "title": title,
            "description": description,
            "price": price,
            "stock": stock,
            "status": status,
            "category": category,
            "condition": condition,
            "imageUrls": image_urls or [],
        })

    def delete_product(self, product_id: int) -> requests.Response:
        return self.delete(f"{self.PREFIX}/products/{product_id}")

    def get_my_products(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/products/me")

    def change_product_status(self, product_id: int, action: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/products/{product_id}/status", json={
            "action": action,
        })

    # ---- Transactions ----

    def get_my_orders(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/transactions/me")

    def get_my_sales(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/transactions/sales")

    def create_order(self, product_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/transactions", json={
            "productID": product_id,
        })

    def pay_order(self, order_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/transactions/{order_id}/pay")

    def confirm_receipt(self, order_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/transactions/{order_id}/confirm-receipt")

    def cancel_order(self, order_id: int) -> requests.Response:
        return self.post(f"{self.PREFIX}/transactions/{order_id}/cancel")

    def get_order_messages(self, order_id: int) -> requests.Response:
        return self.get(f"{self.PREFIX}/transactions/{order_id}/messages")

    def send_order_message(self, order_id: int, content: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/transactions/{order_id}/messages", json={
            "content": content,
        })

    # ---- Disputes ----

    def get_disputes(self) -> requests.Response:
        return self.get(f"{self.PREFIX}/disputes")

    def create_dispute(self, transaction_id: int, reason: str) -> requests.Response:
        return self.post(f"{self.PREFIX}/disputes/transactions/{transaction_id}", json={
            "reason": reason,
        })

    def resolve_dispute(self, dispute_id: int, decision: str, refund_amount: float = 0) -> requests.Response:
        return self.post(f"{self.PREFIX}/disputes/{dispute_id}/resolve", json={
            "decision": decision,
            "refundAmount": refund_amount,
        })
