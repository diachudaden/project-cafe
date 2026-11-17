#!/usr/bin/env python3
"""Send a synthetic, correctly signed SePay-compatible webhook to a configured API."""

import hashlib
import hmac
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone


def setting(*names):
    for name in names:
        value = os.environ.get(name)
        if value:
            return value
    return None


def required(*names):
    value = setting(*names)
    if not value:
        raise ValueError(f"Set environment variable {names[0]}.")
    return value


def main():
    url = setting("SEPAY_WEBHOOK_URL") or "http://localhost:5000/api/hooks/sepay-payment"
    secret = required("Payment__WebhookSecret", "PAYMENT__WEBHOOKSECRET")
    account_number = required("Payment__AccountNumber", "PAYMENT__ACCOUNTNUMBER")
    code = required("PAYMENT_CODE")
    amount = int(required("PAYMENT_AMOUNT"))
    if amount <= 0:
        raise ValueError("PAYMENT_AMOUNT must be greater than zero.")

    transaction_id = setting("SEPAY_TRANSACTION_ID") or f"synthetic-{uuid.uuid4()}"
    timestamp = str(int(time.time()))
    vietnam_time = datetime.now(timezone.utc).astimezone(timezone(timedelta(hours=7)))
    body = json.dumps(
        {
            "id": transaction_id,
            "gateway": "Synthetic",
            "transactionDate": vietnam_time.strftime("%Y-%m-%d %H:%M:%S"),
            "accountNumber": account_number,
            "subAccount": None,
            "code": code,
            "content": code,
            "transferType": "in",
            "description": "Synthetic test transfer",
            "transferAmount": amount,
            "accumulated": amount,
            "referenceCode": transaction_id,
        },
        separators=(",", ":"),
    ).encode("utf-8")
    digest = hmac.new(
        secret.encode("utf-8"),
        timestamp.encode("utf-8") + b"." + body,
        hashlib.sha256,
    ).hexdigest()

    request = urllib.request.Request(
        url,
        data=body,
        headers={
            "Content-Type": "application/json",
            "X-SePay-Timestamp": timestamp,
            "X-SePay-Signature": f"sha256={digest}",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            print(f"HTTP {response.status}")
            print(response.read().decode("utf-8", errors="replace"))
            return 0 if 200 <= response.status < 300 else 1
    except urllib.error.HTTPError as error:
        print(f"HTTP {error.code}")
        print(error.read().decode("utf-8", errors="replace"))
        return 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1)
