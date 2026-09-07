#!/usr/bin/env python3
"""Diagnose HTTP 404 on admin confirm-stock in TESTE.

Reproduces exactly what the admin SPA does:
  GET  /api/auth/csrf
  POST /api/auth/admin/login
  GET  /api/admin/orders?paidOnly=true
  GET  /api/admin/orders/{id}
  POST /api/admin/orders/{id}/fulfillment/confirm-stock

Prints status + raw body for each step. No secrets are printed.
Requires ADMIN_EMAIL / ADMIN_PASSWORD in the environment.
"""
from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request
from http.cookiejar import CookieJar

BASE = os.environ.get(
    "SHOPFLOW_API_BASE", "https://api-teste.vipassessoriadigital.com.br"
)


class Client:
    def __init__(self) -> None:
        self.jar = CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.jar)
        )
        self.csrf: str | None = None

    def request(self, method: str, path: str, payload=None):
        url = f"{BASE}{path}"
        data = json.dumps(payload).encode() if payload is not None else None
        headers = {"Accept": "application/json"}
        if data is not None:
            headers["Content-Type"] = "application/json"
        if method != "GET":
            headers["X-CSRF-TOKEN"] = self.csrf or ""
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with self.opener.open(req, timeout=45) as resp:
                return resp.status, resp.read().decode("utf-8", "replace")
        except urllib.error.HTTPError as err:
            return err.code, err.read().decode("utf-8", "replace")
        except Exception as err:  # noqa: BLE001
            return 0, f"transport_error: {type(err).__name__}: {err}"

    def load_csrf(self) -> None:
        code, body = self.request("GET", "/api/auth/csrf")
        if code == 200:
            self.csrf = json.loads(body).get("token")
        print(f"[csrf] status={code} token_present={bool(self.csrf)}")


def show(label: str, code: int, body: str, limit: int = 700) -> None:
    snippet = body if len(body) <= limit else body[:limit] + "…(truncated)"
    print(f"\n[{label}] status={code}\nbody: {snippet or '<empty body>'}")


def main() -> int:
    email = os.environ.get("ADMIN_EMAIL", "")
    password = os.environ.get("ADMIN_PASSWORD", "")
    if not email or not password:
        print("ADMIN_EMAIL/ADMIN_PASSWORD required", file=sys.stderr)
        return 2

    c = Client()
    c.load_csrf()

    code, body = c.request(
        "POST", "/api/auth/admin/login", {"email": email, "password": password}
    )
    print(f"[login] status={code} ok={code in (200, 204)}")
    if code not in (200, 204):
        show("login-body", code, body)
        return 2
    c.load_csrf()

    code, body = c.request("GET", "/api/admin/orders?page=1&pageSize=50&paidOnly=true")
    if code != 200:
        show("orders-list", code, body)
        return 2
    payload = json.loads(body)
    orders = payload.get("items") or payload.get("data") or []
    print(f"\n[orders-list] status=200 count={len(orders)}")

    target = None
    for o in orders:
        detail_code, detail_body = c.request("GET", f"/api/admin/orders/{o.get('id')}")
        if detail_code != 200:
            continue
        d = json.loads(detail_body)
        print(
            f"  order={d.get('orderNumber')} id={d.get('id')} "
            f"status={d.get('status')} fulfillment={d.get('fulfillmentStatus')} "
            f"stockConfirmedAt={d.get('stockConfirmedAt')} "
            f"canConfirmStock={d.get('canConfirmStock')} "
            f"canMarkAsSeparated={d.get('canMarkAsSeparated')}"
        )
        if target is None and not d.get("stockConfirmedAt"):
            target = d

    if target is None:
        print("\nno paid order without stockConfirmedAt found; nothing to reproduce")
        return 0

    oid = target["id"]
    print(f"\n=== reproducing SPA call on order {target.get('orderNumber')} ({oid}) ===")

    code, body = c.request(
        "POST", f"/api/admin/orders/{oid}/fulfillment/confirm-stock", {"note": None}
    )
    show("confirm-stock", code, body)

    if code == 200:
        after = json.loads(body)
        print(
            f"\n[after] fulfillmentStatus={after.get('fulfillmentStatus')} "
            f"stockConfirmedAt={after.get('stockConfirmedAt')} "
            f"canConfirmStock={after.get('canConfirmStock')} "
            f"canMarkAsSeparated={after.get('canMarkAsSeparated')}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
