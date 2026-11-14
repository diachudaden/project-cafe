import { describe, expect, it } from "vitest";
import { ApiError, createHttp } from "./http";

describe("createHttp errors", () => {
  it("keeps the status and ProblemDetails traceId of a server error (TASK-026)", async () => {
    const body = { type: "https://tools.ietf.org/html/rfc9110#section-15.6.1", title: "An error occurred while processing your request.", status: 500, traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00" };
    const http = createHttp({ fetch: async () => new Response(JSON.stringify(body), { status: 500, headers: { "Content-Type": "application/problem+json" } }) });

    const error = await http.request("GET", "/api/branches/x/products").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(500);
    expect((error as ApiError).traceId).toBe(body.traceId);
  });
});

describe("createHttp download", () => {
  it("sends the bearer token as a header (never in the URL) and returns the PDF blob", async () => {
    const calls: { url: string; headers: Record<string, string> }[] = [];
    const http = createHttp({
      getAccessToken: () => "token-123",
      fetch: async (input, init) => {
        calls.push({ url: String(input), headers: init?.headers as Record<string, string> });
        return new Response(new Uint8Array([0x25, 0x50, 0x44, 0x46]), { status: 200, headers: { "Content-Type": "application/pdf" } });
      },
    });

    const blob = await http.download("/api/bills/QW12ER34/receipt.pdf");

    expect(blob.size).toBe(4);
    expect(calls[0].url).toBe("/api/bills/QW12ER34/receipt.pdf");
    expect(calls[0].url).not.toContain("token");
    expect(calls[0].headers.Authorization).toBe("Bearer token-123");
    expect(calls[0].headers.Accept).toContain("application/pdf");
  });

  it("turns a 404 problem into an ApiError", async () => {
    const http = createHttp({ fetch: async () => new Response(JSON.stringify({ status: 404, detail: "Không tìm thấy hóa đơn." }), { status: 404 }) });
    await expect(http.download("/api/bills/X/receipt.pdf")).rejects.toMatchObject({ status: 404, message: "Không tìm thấy hóa đơn." });
  });
});
