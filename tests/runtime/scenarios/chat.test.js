import { createExecutionContext, env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/ChatRoom/dist/worker.js";

const invoke = (path, init) => worker.fetch(
  new Request(`https://worker.test${path}`, init),
  env,
  createExecutionContext(),
);

const nextMessage = socket => new Promise(resolve =>
  socket.addEventListener("message", event => resolve(JSON.parse(event.data)), { once: true }),
);

describe("hibernating Durable Object chat scenario", () => {
  it("validates routes and WebSocket upgrades", async () => {
    expect((await invoke("/invalid")).status).toBe(404);
    expect((await invoke("/rooms/general")).status).toBe(426);
  });

  it("accepts a hibernating socket, persists attachments, and answers messages", async () => {
    const response = await invoke(`/rooms/room-${crypto.randomUUID()}?name=Ada`, {
      headers: { upgrade: "websocket" },
    });
    expect(response.status).toBe(101);
    const socket = response.webSocket;
    socket.accept();

    await expect(nextMessage(socket)).resolves.toMatchObject({ type: "welcome", name: "Ada", online: 1 });
    const users = nextMessage(socket);
    socket.send("who");
    await expect(users).resolves.toEqual({ type: "users", users: ["Ada"] });

    const pong = new Promise(resolve =>
      socket.addEventListener("message", event => resolve(event.data), { once: true }),
    );
    socket.send("ping");
    await expect(pong).resolves.toBe("pong");
    socket.close(1000, "done");
  });
});
