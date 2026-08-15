import { env } from "cloudflare:workers";
import { afterEach, vi } from "vitest";

afterEach(() => vi.restoreAllMocks());

export { env };
