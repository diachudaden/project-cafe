#!/usr/bin/env node
// Gửi một webhook SePay giả, ký HMAC đúng như SePay, tới API local để test luồng end-to-end
// mà không cần chuyển tiền thật.
//
//   node tools/payments/send-fake-webhook.mjs --code CR12 --amount 25000
//   node tools/payments/send-fake-webhook.mjs --code CR12 --amount 25000 --id 555 --repeat 3   # thử chống trùng
//   node tools/payments/send-fake-webhook.mjs --content "ck tien cafe" --amount 25000          # thử Unmatched
//
// Biến môi trường:
//   SEPAY_WEBHOOK_URL     mặc định http://localhost:8080/api/webhooks/sepay
//   SEPAY_WEBHOOK_SECRET  bắt buộc, trùng Payment:WebhookSecret của API
//   SEPAY_ACCOUNT         mặc định 0123456789, trùng Payment:AccountNumber

import { createHmac } from 'node:crypto';
import { parseArgs } from 'node:util';

const { values: args } = parseArgs({
  options: {
    code: { type: 'string' },
    amount: { type: 'string', default: '25000' },
    content: { type: 'string' },
    id: { type: 'string' },
    repeat: { type: 'string', default: '1' },
    url: { type: 'string', default: process.env.SEPAY_WEBHOOK_URL ?? 'http://localhost:8080/api/webhooks/sepay' },
    account: { type: 'string', default: process.env.SEPAY_ACCOUNT ?? '0123456789' },
    'bad-signature': { type: 'boolean', default: false },
  },
});

const secret = process.env.SEPAY_WEBHOOK_SECRET;
if (!secret) {
  console.error('Thiếu SEPAY_WEBHOOK_SECRET');
  process.exit(1);
}

/** Ký giống SePay: HMAC-SHA256(secret, `${timestamp}.${rawBody}`), hex thường. */
export function sign(secretKey, timestamp, rawBody) {
  return createHmac('sha256', secretKey).update(`${timestamp}.${rawBody}`).digest('hex');
}

/** Giờ Việt Nam dạng "yyyy-MM-dd HH:mm:ss" như SePay gửi. */
function vietnamNow() {
  const d = new Date(Date.now() + 7 * 3600 * 1000);
  return d.toISOString().slice(0, 19).replace('T', ' ');
}

const amount = Number(args.amount);
const payload = {
  id: Number(args.id ?? Date.now() % 1_000_000_000),
  gateway: 'MBBank',
  transactionDate: vietnamNow(),
  accountNumber: args.account,
  subAccount: '',
  code: args.code ?? null,
  content: args.content ?? `${args.code ?? ''} chuyen tien`.trim(),
  transferType: 'in',
  description: 'TEST chuyen tien',
  transferAmount: amount,
  accumulated: 0,
  referenceCode: `FT${Date.now()}`,
};

const body = JSON.stringify(payload);

for (let i = 1; i <= Number(args.repeat); i++) {
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const signature = args['bad-signature'] ? 'sha256=' + '0'.repeat(64) : `sha256=${sign(secret, timestamp, body)}`;

  const res = await fetch(args.url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-SePay-Signature': signature,
      'X-SePay-Timestamp': timestamp,
    },
    body,
  });

  const text = await res.text();
  console.log(`#${i} id=${payload.id} code=${payload.code} amount=${amount} → ${res.status} ${text}`);
}
