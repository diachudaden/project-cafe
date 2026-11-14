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
