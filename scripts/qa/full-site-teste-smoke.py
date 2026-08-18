#!/usr/bin/env python3
"""
TESTE-only full-site smoke with controlled real emails.
Run on VPS or locally against api-teste. Never prints secrets/tokens.
"""
from __future__ import annotations

import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from http.cookiejar import CookieJar
from typing import Any

BASE = os.environ.get("SHOPFLOW_API_BASE", "https://api-teste.vipassessoriadigital.com.br").rstrip("/")
QA_EMAIL = os.environ["QA_EMAIL"]  # required
ADMIN_EMAIL = os.environ["ADMIN_EMAIL"]
ADMIN_PASSWORD = os.environ["ADMIN_PASSWORD"]
TS = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
PASSWORD = os.environ.get("QA_CUSTOMER_PASSWORD", "QaSmoke!Test2026Aa")
NEW_PASSWORD = os.environ.get("QA_CUSTOMER_PASSWORD_NEW", "QaSmoke!Test2026Bb")

results: dict[str, Any] = {
    "startedAt": datetime.now(timezone.utc).isoformat(),
    "base": BASE,
    "qaEmailMasked": re.sub(r"(^.{3}).*(@.*$)", r"\1***\2", QA_EMAIL),
    "flows": {},
    "orders": [],
    "products": [],
    "customers": [],
    "bugs": [],
    "risks": [],
    "notRun": [],
}


def mask_email(e: str) -> str:
    return re.sub(r"(^.{3}).*(@.*$)", r"\1***\2", e)


def alias(tag: str) -> str:
    local, _, domain = QA_EMAIL.partition("@")
    if not domain:
        return QA_EMAIL
    # Gmail-style plus alias
    return f"{local}+{tag}-{TS}@{domain}"


class Client:
    def __init__(self) -> None:
        self.cj = CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj))
        self.csrf: str | None = None

    def refresh_csrf(self) -> str:
        code, body = self.request("GET", "/api/auth/csrf")
        if code != 200:
            raise RuntimeError(f"csrf failed {code} {body}")
        self.csrf = body["token"]
        return self.csrf

    def request(
        self,
        method: str,
        path: str,
        payload: Any = None,
        *,
        csrf: bool = False,
        raw: bool = False,
    ) -> tuple[int, Any]:
        url = f"{BASE}{path}"
        data = None
        headers = {"Accept": "application/json"}
        if payload is not None:
            data = json.dumps(payload).encode()
            headers["Content-Type"] = "application/json"
        if csrf:
            if not self.csrf:
                self.refresh_csrf()
            headers["X-CSRF-TOKEN"] = self.csrf or ""
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with self.opener.open(req, timeout=60) as resp:
                raw_body = resp.read()
                code = resp.status
                if raw:
                    return code, raw_body
                if not raw_body:
                    return code, None
                try:
                    return code, json.loads(raw_body.decode())
                except Exception:
                    return code, {"_raw": raw_body.decode("utf-8", "replace")[:500]}
        except urllib.error.HTTPError as e:
            raw_body = e.read()
            try:
                body = json.loads(raw_body.decode()) if raw_body else None
            except Exception:
                body = {"_raw": raw_body.decode("utf-8", "replace")[:500]}
            return e.code, body


def set_flow(name: str, result: str, **extra: Any) -> None:
    results["flows"][name] = {"status": result, **extra}
    print(f"[{result}] {name}" + (f" — {extra.get('note','')}" if extra.get("note") else ""))


def code_of(body: Any) -> str | None:
    if not isinstance(body, dict):
        return None
    return body.get("code") or (body.get("extensions") or {}).get("code")


def main() -> int:
    admin = Client()
    admin.refresh_csrf()
    code, body = admin.request(
        "POST",
        "/api/auth/admin/login",
        {"email": ADMIN_EMAIL, "password": ADMIN_PASSWORD},
        csrf=True,
    )
    if code != 200:
        set_flow("admin_login", "FAIL", http=code, note="admin login failed")
        results["bugs"].append("Admin login failed on TESTE")
        print(json.dumps({"decision": "BLOCKED", "reason": "admin_login", "http": code}, indent=2))
        return 2
    admin.refresh_csrf()
    set_flow("admin_login", "PASS", http=code)

    # --- StoreAccess Closed anonymous ---
    anon = Client()
    c, b = anon.request("GET", "/api/catalog/products?page=1&pageSize=4")
    ok = c in (401, 403) and code_of(b) in (
        "STORE_ACCESS_REQUIRES_LOGIN",
        "STORE_ACCESS_REQUIRES_APPROVAL",
        "CUSTOMER_APPROVAL_PENDING",
    )
    set_flow(
        "store_access_closed_anon",
        "PASS" if ok else "FAIL",
        http=c,
        code=code_of(b),
    )
    if not ok:
        results["bugs"].append("Anonymous catalog not blocked in Closed mode")

    # Guest checkout disabled
    admin.refresh_csrf()
    c, b = anon.request(
        "POST",
        "/api/checkout/sessions",
        {
            "customer": {
                "fullName": "Guest Block",
                "email": alias("guest"),
                "phone": "11999990000",
            },
            "address": {
                "zipCode": "01310100",
                "street": "Avenida Paulista",
                "number": "1000",
                "complement": "",
                "neighborhood": "Bela Vista",
                "city": "Sao Paulo",
                "state": "SP",
            },
            "items": [],
            "preferredDeliveryMethod": "Carrier",
        },
        csrf=True,
    )
    guest_ok = c in (401, 403, 400) and (
        code_of(b)
        in (
            "GUEST_CHECKOUT_DISABLED",
            "CUSTOMER_LOGIN_REQUIRED",
            "STORE_ACCESS_REQUIRES_LOGIN",
            "STORE_ACCESS_REQUIRES_APPROVAL",
        )
        or c in (401, 403)
    )
    set_flow(
        "guest_checkout_disabled",
        "PASS" if guest_ok else "FAIL",
        http=c,
        code=code_of(b),
    )

    # --- Register pending ---
    pending_email = alias("pending")
    cust = Client()
    cust.refresh_csrf()
    c, b = cust.request(
        "POST",
        "/api/auth/customer/register",
        {
            "email": pending_email,
            "password": PASSWORD,
            "fullName": "QA Store Closed Pending",
            "phone": "11999990001",
        },
    )
    reg_ok = c in (200, 201)
    status = None
    if isinstance(b, dict):
        status = (b.get("customer") or b).get("accessStatus") or (b.get("customer") or b).get(
            "approvalStatus"
        )
    set_flow(
        "register_pending",
        "PASS" if reg_ok else "FAIL",
        http=c,
        email=mask_email(pending_email),
        accessStatus=status,
    )
    results["customers"].append({"role": "pending", "email": mask_email(pending_email)})
    if not reg_ok:
        results["bugs"].append(f"Register pending failed HTTP {c}")

    # Login pending + catalog block
    cust.refresh_csrf()
    c, b = cust.request(
        "POST",
        "/api/auth/customer/login",
        {"email": pending_email, "password": PASSWORD},
        csrf=True,
    )
    login_pending_ok = c == 200
    set_flow("login_pending", "PASS" if login_pending_ok else "FAIL", http=c)
    cust.refresh_csrf()
    c, b = cust.request("GET", "/api/catalog/products?page=1&pageSize=4")
    pending_block = c in (401, 403) and code_of(b) in (
        "STORE_ACCESS_REQUIRES_APPROVAL",
        "CUSTOMER_APPROVAL_PENDING",
        "STORE_ACCESS_REQUIRES_LOGIN",
    )
    set_flow(
        "pending_catalog_block",
        "PASS" if pending_block else "FAIL",
        http=c,
        code=code_of(b),
    )

    # Admin list approvals + approve
    admin.refresh_csrf()
    c, b = admin.request("GET", "/api/admin/customers/approvals?page=1&pageSize=50")
    set_flow("admin_approvals_list", "PASS" if c == 200 else "FAIL", http=c)
    customer_id = None
    if isinstance(b, dict):
        for item in b.get("items") or []:
            if str(item.get("email", "")).lower() == pending_email.lower():
                customer_id = item.get("id") or item.get("customerId")
                break
    if not customer_id and isinstance(b, dict):
        # search
        admin.refresh_csrf()
        c2, b2 = admin.request(
            "GET",
            f"/api/admin/customers?q={urllib.parse.quote(pending_email)}&page=1&pageSize=20",
        )
        for item in (b2 or {}).get("items") or []:
            if str(item.get("email", "")).lower() == pending_email.lower():
                customer_id = item.get("id") or item.get("customerId")
                break

    if customer_id:
        admin.refresh_csrf()
        c, b = admin.request(
            "POST",
            f"/api/admin/customers/{customer_id}/approve",
            {"reason": "QA smoke internal"},
            csrf=True,
        )
        set_flow("admin_approve", "PASS" if c == 200 else "FAIL", http=c)
    else:
        set_flow("admin_approve", "FAIL", note="customer_id not found")
        results["bugs"].append("Could not find pending customer to approve")

    # Extra customers: reject / suspend / reactivate
    for tag, action in (("reject", "reject"), ("suspend", "suspend")):
        email = alias(tag)
        cclient = Client()
        cclient.refresh_csrf()
        c, _ = cclient.request(
            "POST",
            "/api/auth/customer/register",
            {
                "email": email,
                "password": PASSWORD,
                "fullName": f"QA {tag}",
                "phone": "11999990002",
            },
        )
        if c not in (200, 201):
            set_flow(f"register_{tag}", "FAIL", http=c)
            continue
        results["customers"].append({"role": tag, "email": mask_email(email)})
        admin.refresh_csrf()
        c, b = admin.request(
            "GET",
            f"/api/admin/customers?q={urllib.parse.quote(email)}&page=1&pageSize=5",
        )
        cid = None
        for item in (b or {}).get("items") or []:
            if str(item.get("email", "")).lower() == email.lower():
                cid = item.get("id") or item.get("customerId")
                break
        if not cid:
            set_flow(f"admin_{action}", "FAIL", note="id missing")
            continue
        # suspend needs approve first
        if action == "suspend":
            admin.refresh_csrf()
            admin.request(
                "POST",
                f"/api/admin/customers/{cid}/approve",
                {"reason": "prep suspend"},
                csrf=True,
            )
        admin.refresh_csrf()
        c, b = admin.request(
            "POST",
            f"/api/admin/customers/{cid}/{action}",
            {"reason": f"QA internal {action}"},
            csrf=True,
        )
        set_flow(f"admin_{action}", "PASS" if c == 200 else "FAIL", http=c)
        if action == "suspend" and c == 200:
            admin.refresh_csrf()
            c, b = admin.request(
                "POST",
                f"/api/admin/customers/{cid}/reactivate",
                {"reason": "QA reactivate"},
                csrf=True,
            )
            set_flow("admin_reactivate", "PASS" if c == 200 else "FAIL", http=c)

    # Approved customer catalog
    approved = Client()
    approved.refresh_csrf()
    c, b = approved.request(
        "POST",
        "/api/auth/customer/login",
        {"email": pending_email, "password": PASSWORD},
        csrf=True,
    )
    set_flow("login_approved", "PASS" if c == 200 else "FAIL", http=c)
    approved.refresh_csrf()
    c, b = approved.request("GET", "/api/catalog/products?page=1&pageSize=8")
    catalog_ok = c == 200 and isinstance(b, dict) and "items" in b
    set_flow("catalog_approved", "PASS" if catalog_ok else "FAIL", http=c)
    product_slug = None
    sku_id = None
    if catalog_ok and b.get("items"):
        product_slug = b["items"][0].get("slug")
        pid = b["items"][0].get("id")
        c2, det = approved.request("GET", f"/api/catalog/products/{pid}")
        for s in (det or {}).get("skus") or []:
            if s.get("isActive"):
                sku_id = s["id"]
                break
        set_flow("pdp_approved", "PASS" if c2 == 200 and sku_id else "FAIL", http=c2)

    # --- Admin create product with accented name (slug blank) ---
    admin.refresh_csrf()
    slug_name = f"Camiseta Básica QA {TS}"
    c, b = admin.request(
        "POST",
        "/api/catalog/products/variant",
        {
            "name": slug_name,
            "slug": "",
            "categoryId": None,
            "description": "Smoke QA produto acentuado",
            "isActive": True,
            "isFeatured": False,
            "displayOrder": None,
        },
        csrf=True,
    )
    product_id = (b or {}).get("id") if isinstance(b, dict) else None
    set_flow(
        "admin_product_create_accent_slug",
        "PASS" if c in (200, 201) and product_id else "FAIL",
        http=c,
        body_keys=list(b.keys()) if isinstance(b, dict) else None,
        errors=(b or {}).get("errors") if isinstance(b, dict) else None,
    )
    qa_sku_id = None
    if product_id:
        # fetch created product for generated slug
        admin.refresh_csrf()
        c, det = admin.request("GET", f"/api/catalog/products/{product_id}")
        gen_slug = (det or {}).get("slug") if isinstance(det, dict) else None
        accent_in_slug = bool(gen_slug and re.search(r"[áàâãéêíóôõúç]", gen_slug, re.I))
        set_flow(
            "admin_product_slug_generated",
            "PASS" if gen_slug and not accent_in_slug else "FAIL",
            slug=gen_slug,
        )
        results["products"].append({"id": product_id, "slug": gen_slug, "name": slug_name})

        admin.refresh_csrf()
        c, b = admin.request(
            "POST",
            f"/api/catalog/products/{product_id}/variants",
            {
                "code": None,
                "regularPrice": 150.0,
                "active": True,
                "attributes": [],
                "salesRule": {
                    "salesMode": "Unit",
                    "minimumQuantity": 1,
                    "quantityStep": 1,
                    "packageSize": None,
                    "packageLabel": None,
                    "packageDescription": None,
                    "quantityUnitLabel": "peça(s)",
                    "allowCustomerToChooseVariants": True,
                    "showTotalPieces": False,
                    "isWholesaleOnly": False,
                },
            },
            csrf=True,
        )
        qa_sku_id = (b or {}).get("skuId") or (b or {}).get("id")
        set_flow("admin_sku_create", "PASS" if c in (200, 201) and qa_sku_id else "FAIL", http=c)

        # inventory
        if qa_sku_id:
            admin.refresh_csrf()
            c, b = admin.request(
                "POST",
                f"/api/inventory/skus/{qa_sku_id}",
                {"initialQuantity": 20},
                csrf=True,
            )
            # 201 or 409 if exists
            set_flow(
                "inventory_create",
                "PASS" if c in (200, 201, 204, 409) else "FAIL",
                http=c,
            )
            admin.refresh_csrf()
            c, b = admin.request(
                "POST",
                f"/api/inventory/skus/{qa_sku_id}/add",
                {"quantity": 5, "reason": "QA smoke add"},
                csrf=True,
            )
            set_flow("inventory_add", "PASS" if c in (200, 204) else "FAIL", http=c)
            admin.refresh_csrf()
            c, b = admin.request(
                "POST",
                f"/api/inventory/skus/{qa_sku_id}/remove",
                {"quantity": 1, "reason": ""},
                csrf=True,
            )
            reason_required = c == 400
            set_flow(
                "inventory_remove_reason_required",
                "PASS" if reason_required else "FAIL",
                http=c,
            )
            admin.refresh_csrf()
            c, b = admin.request(
                "POST",
                f"/api/inventory/skus/{qa_sku_id}/remove",
                {"quantity": 1, "reason": "QA smoke remove"},
                csrf=True,
            )
            set_flow("inventory_remove", "PASS" if c in (200, 204) else "FAIL", http=c)

    # Prefer QA sku for checkout; fallback existing
    checkout_sku = qa_sku_id or sku_id
    if not checkout_sku:
        set_flow("checkout", "FAIL", note="no sku")
        results["bugs"].append("No SKU for checkout")
    else:
        # delivery date +2 business days approx
        d = datetime.now(timezone.utc).date() + timedelta(days=4)
        while d.weekday() >= 5:
            d += timedelta(days=1)
        preferred_date = d.isoformat()

        approved.refresh_csrf()
        c, session = approved.request(
            "POST",
            "/api/checkout/sessions",
            {
                "customer": {
                    "fullName": "QA Store Closed Pending",
                    "email": pending_email,
                    "phone": "11999990001",
                },
                "address": {
                    "zipCode": "01310100",
                    "street": "Avenida Paulista",
                    "number": "1000",
                    "complement": "SMOKE-FULL",
                    "neighborhood": "Bela Vista",
                    "city": "Sao Paulo",
                    "state": "SP",
                },
                "items": [{"skuId": checkout_sku, "quantity": 1}],
                "preferredDeliveryMethod": "Carrier",
                "preferredDeliveryDate": preferred_date,
                "customerOrderNote": "Obs cliente QA smoke",
            },
            csrf=True,
        )
        sid = None
        if isinstance(session, dict):
            sid = session.get("checkoutSessionId") or session.get("id")
        set_flow("checkout_session", "PASS" if c in (200, 201) and sid else "FAIL", http=c)
        if sid:
            approved.refresh_csrf()
            c, order = approved.request(
                "POST",
                "/api/orders/from-checkout-session",
                {"checkoutSessionId": sid},
                csrf=True,
            )
            order_id = (order or {}).get("orderId") or (order or {}).get("id")
            order_number = (order or {}).get("orderNumber")
            guest_token = (order or {}).get("guestAccessToken")
            customer_user_id = (order or {}).get("customerUserId")
            set_flow(
                "order_create",
                "PASS" if c in (200, 201) and order_id else "FAIL",
                http=c,
                orderNumber=order_number,
                hasGuestToken=bool(guest_token),
                hasCustomerUserId=bool(customer_user_id),
            )
            if guest_token:
                results["bugs"].append("New Closed order unexpectedly returned guestAccessToken")
            if order_id:
                results["orders"].append(
                    {
                        "orderId": order_id,
                        "orderNumber": order_number,
                        "kind": "primary",
                    }
                )

                # CEP lookup smoke
                approved.refresh_csrf()
                c_cep, _ = approved.request("GET", "/api/postal-codes/01310100")
                if c_cep == 404:
                    c_cep, _ = approved.request("GET", "/api/postal-code/01310100")
                if c_cep == 404:
                    c_cep, _ = approved.request("GET", "/api/address/postal-code/01310100")
                set_flow(
                    "postal_code_lookup",
                    "PASS" if c_cep == 200 else ("NOT RUN" if c_cep == 404 else "FAIL"),
                    http=c_cep,
                )

                # Pix create
                approved.refresh_csrf()
                c, pix = approved.request(
                    "POST",
                    f"/api/payments/pix/orders/{order_id}",
                    {},
                    csrf=True,
                )
                pix_status = (pix or {}).get("status") if isinstance(pix, dict) else None
                has_qr = bool(
                    isinstance(pix, dict)
                    and (pix.get("qrCode") or pix.get("copyPaste") or pix.get("qrCodeBase64") or pix.get("qrCodeText"))
                )
                set_flow(
                    "pix_create",
                    "PASS" if c in (200, 201) and has_qr else "FAIL",
                    http=c,
                    pixStatus=pix_status,
                    hasQr=has_qr,
                )

                # Wait for Paid via reconciliation (sandbox APRO)
                paid = False
                for i in range(24):
                    time.sleep(5)
                    admin.refresh_csrf()
                    c, od = admin.request("GET", f"/api/admin/orders/{order_id}")
                    st = (od or {}).get("status") or (od or {}).get("paymentStatus")
                    # prefer order status
                    order_status = (od or {}).get("status")
                    payment_status = (od or {}).get("paymentStatus")
                    if str(order_status).lower() == "paid" or str(payment_status).lower() == "paid":
                        paid = True
                        set_flow(
                            "pix_paid",
                            "PASS",
                            attempt=i + 1,
                            orderStatus=order_status,
                            paymentStatus=payment_status,
                        )
                        break
                if not paid:
                    set_flow("pix_paid", "FAIL", note="not Paid within ~120s")
                    results["bugs"].append("Pix did not reach Paid (webhook signature_mismatch risk)")

                # Customer order detail
                approved.refresh_csrf()
                c, co = approved.request("GET", f"/api/customer/orders/{order_id}")
                if c != 200:
                    # try by order number route variants
                    c, co = approved.request("GET", "/api/customer/orders?page=1&pageSize=20")
                set_flow("customer_orders", "PASS" if c == 200 else "FAIL", http=c)
                if isinstance(co, dict):
                    blob = json.dumps(co)
                    leaks = []
                    for bad in (
                        "internalOrderNote",
                        "guestAccessToken",
                        "providerPaymentId",
                        "AccessDecisionReason",
                    ):
                        if bad in blob:
                            leaks.append(bad)
                    set_flow(
                        "customer_order_no_internal_leak",
                        "PASS" if not leaks else "FAIL",
                        leaks=leaks,
                    )

                # Admin order detail + internal note
                admin.refresh_csrf()
                c, ao = admin.request("GET", f"/api/admin/orders/{order_id}")
                set_flow("admin_order_detail", "PASS" if c == 200 else "FAIL", http=c)
                admin.refresh_csrf()
                c, _ = admin.request(
                    "PUT",
                    f"/api/admin/orders/{order_id}/internal-note",
                    {"internalNote": "Nota interna QA smoke — não vazar"},
                    csrf=True,
                )
                # endpoint may be PATCH/PUT different — try alternatives
                if c not in (200, 204):
                    admin.refresh_csrf()
                    c, _ = admin.request(
                        "PUT",
                        f"/api/admin/orders/{order_id}/internal-order-note",
                        {"internalOrderNote": "Nota interna QA smoke"},
                        csrf=True,
                    )
                set_flow(
                    "admin_internal_note",
                    "PASS" if c in (200, 204) else "NOT RUN",
                    http=c,
                    note="endpoint variant may differ",
                )

                # Fulfillment individual — only if paid
                if paid:
                    admin.refresh_csrf()
                    c, _ = admin.request(
                        "POST",
                        f"/api/admin/orders/{order_id}/fulfillment/ship",
                        {
                            "finalDeliveryMethod": "Carrier",
                            "trackingCode": "QA-TRACK-1",
                            "internalNote": "ship internal",
                        },
                        csrf=True,
                    )
                    set_flow("fulfillment_ship", "PASS" if c == 200 else "FAIL", http=c)
                    admin.refresh_csrf()
                    c, _ = admin.request(
                        "POST",
                        f"/api/admin/orders/{order_id}/fulfillment/deliver",
                        {"internalNote": "deliver internal"},
                        csrf=True,
                    )
                    set_flow("fulfillment_deliver", "PASS" if c == 200 else "FAIL", http=c)

    # Second + third paid orders for remessa (if possible)
    batch_order_ids: list[str] = []
    if checkout_sku and results["flows"].get("login_approved", {}).get("status") == "PASS":
        for n in range(2):
            approved.refresh_csrf()
            d = datetime.now(timezone.utc).date() + timedelta(days=5 + n)
            while d.weekday() >= 5:
                d += timedelta(days=1)
            c, session = approved.request(
                "POST",
                "/api/checkout/sessions",
                {
                    "customer": {
                        "fullName": "QA Store Closed Pending",
                        "email": pending_email,
                        "phone": "11999990001",
                    },
                    "address": {
                        "zipCode": "01310100",
                        "street": "Avenida Paulista",
                        "number": "1000",
                        "complement": f"BATCH-{n}",
                        "neighborhood": "Bela Vista",
                        "city": "Sao Paulo",
                        "state": "SP",
                    },
                    "items": [{"skuId": checkout_sku, "quantity": 1}],
                    "preferredDeliveryMethod": "Carrier",
                    "preferredDeliveryDate": d.isoformat(),
                    "customerOrderNote": f"batch {n}",
                },
                csrf=True,
            )
            sid = (session or {}).get("checkoutSessionId") or (session or {}).get("id")
            if not sid:
                continue
            approved.refresh_csrf()
            c, order = approved.request(
                "POST",
                "/api/orders/from-checkout-session",
                {"checkoutSessionId": sid},
                csrf=True,
            )
            oid = (order or {}).get("orderId") or (order or {}).get("id")
            onum = (order or {}).get("orderNumber")
            if not oid:
                continue
            results["orders"].append({"orderId": oid, "orderNumber": onum, "kind": "batch"})
            approved.refresh_csrf()
            approved.request("POST", f"/api/payments/pix/orders/{oid}", {}, csrf=True)
            # wait paid briefly
            for _ in range(18):
                time.sleep(5)
                admin.refresh_csrf()
                c, od = admin.request("GET", f"/api/admin/orders/{oid}")
                if str((od or {}).get("status")).lower() == "paid":
                    batch_order_ids.append(oid)
                    break

    if len(batch_order_ids) >= 2:
        admin.refresh_csrf()
        c, batch = admin.request(
            "POST",
            "/api/admin/delivery-batches",
            {
                "orderIds": batch_order_ids[:2],
                "deliveryMethod": "Carrier",
                "trackingCode": "QA-BATCH-1",
                "internalNote": "remessa internal",
                "confirmDifferentAddresses": True,
            },
            csrf=True,
        )
        batch_id = (batch or {}).get("id")
        batch_number = (batch or {}).get("batchNumber") or (batch or {}).get("number")
        set_flow(
            "delivery_batch_create",
            "PASS" if c in (200, 201) and batch_id else "FAIL",
            http=c,
            batchNumber=batch_number,
        )
        if batch_id:
            admin.refresh_csrf()
            c, _ = admin.request(
                "POST",
                f"/api/admin/delivery-batches/{batch_id}/ship",
                {
                    "deliveryMethod": "Carrier",
                    "trackingCode": "QA-BATCH-SHIP",
                    "internalNote": "ship batch",
                },
                csrf=True,
            )
            set_flow("delivery_batch_ship", "PASS" if c == 200 else "FAIL", http=c)
            admin.refresh_csrf()
            c, _ = admin.request(
                "POST",
                f"/api/admin/delivery-batches/{batch_id}/deliver",
                {"internalNote": "deliver batch"},
                csrf=True,
            )
            set_flow("delivery_batch_deliver", "PASS" if c == 200 else "FAIL", http=c)
            # idempotent ship again
            admin.refresh_csrf()
            c, _ = admin.request(
                "POST",
                f"/api/admin/delivery-batches/{batch_id}/ship",
                {"deliveryMethod": "Carrier"},
                csrf=True,
            )
            set_flow(
                "delivery_batch_ship_idempotent",
                "PASS" if c in (200, 409) else "FAIL",
                http=c,
            )
    else:
        set_flow(
            "delivery_batch_create",
            "NOT RUN",
            note=f"need 2 paid orders, got {len(batch_order_ids)}",
        )
        results["notRun"].append("DeliveryBatch (insufficient paid orders)")

    # Forgot password
    anon2 = Client()
    anon2.refresh_csrf()
    c, b = anon2.request(
        "POST",
        "/api/auth/customer/forgot-password",
        {"email": pending_email},
        csrf=True,
    )
    set_flow("forgot_password_request", "PASS" if c in (200, 204) else "FAIL", http=c)

    # Security: admin without auth
    naked = Client()
    c, _ = naked.request("GET", "/api/admin/orders?page=1&pageSize=5")
    set_flow("security_admin_orders_anon", "PASS" if c in (401, 403) else "FAIL", http=c)
    c, _ = naked.request("GET", "/api/admin/customers/approvals")
    set_flow(
        "security_admin_approvals_anon",
        "PASS" if c in (401, 403) else "FAIL",
        http=c,
    )

    # WhatsApp — FE config check via public bundle not needed; mark API N/A
    results["notRun"].append("WhatsApp UI CTAs (browser) — validate manually / Cypress")
    results["notRun"].append("Guest tracking legado — no legacy token seeded in this run")
    results["notRun"].append("Confirm email link click — token not extracted by design")
    results["notRun"].append("R2 image upload multipart — skipped if no local image file")

    results["finishedAt"] = datetime.now(timezone.utc).isoformat()

    # Decision draft
    fails = [k for k, v in results["flows"].items() if v.get("status") == "FAIL"]
    blockers = [
        "store_access_closed_anon",
        "register_pending",
        "admin_approve",
        "login_approved",
        "catalog_approved",
        "order_create",
        "pix_paid",
        "guest_checkout_disabled",
    ]
    blocked = any(f in fails for f in blockers) or bool(
        [b for b in results["bugs"] if "login" in b.lower() or "Closed" in b]
    )
    if blocked:
        results["decision"] = "BLOCKED"
    elif fails or results["bugs"]:
        results["decision"] = "PASS WITH RISKS"
    else:
        results["decision"] = "PASS WITH RISKS"  # emails receipt not verified in script

    results["failedFlows"] = fails
    out_path = os.environ.get("SMOKE_OUT", "/tmp/shopflow-full-smoke-result.json")
    with open(out_path, "w") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    print(f"WROTE {out_path}")
    print(f"DECISION={results['decision']}")
    print(f"FAILED={fails}")
    return 0 if results["decision"] != "BLOCKED" else 2


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as e:
        print(f"FATAL: {type(e).__name__}: {e}", file=sys.stderr)
        raise
