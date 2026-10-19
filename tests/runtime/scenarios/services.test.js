import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import worker from "../fixtures/ServiceGateway/dist/worker.js";

const invoke = (path, init) => worker.fetch(new Request(`https://worker.test${path}`, init), env, {});

beforeEach(async () => {
  await env.DB.exec("CREATE TABLE IF NOT EXISTS users (id TEXT PRIMARY KEY, username TEXT NOT NULL, created_at TEXT NOT NULL); DELETE FROM users;");
});

describe("named WorkerEntrypoint service bindings", () => {
  it("supports out-variable declarations in generated constructors", async () => {
    await expect(env.CORE.constructorValue()).resolves.toBe(7);
  });

  it("copies value snapshots in generated field initializers", async () => {
    await expect(env.CORE.fieldInitializerSnapshot()).resolves.toBe(true);
  });

  it("copies value snapshots before completing async expression returns", async () => {
    await expect(env.CORE.asyncReturnSnapshot()).resolves.toBe(3);
  });

  it("preserves export names that collide with native JavaScript globals", async () => {
    await expect(env.OBJECT.ping()).resolves.toBe("6:4");
    await expect(env.RESERVED_FIELDS.ping()).resolves.toBe(10);
    await expect(env.RESERVED_FIELDS.core()).resolves.toBe("user:Ada");
  });

  it("transports numeric dictionaries and structural sets through native RPC", async () => {
    const map = await env.CORE.buildNumberMap();
    expect(map).toBeInstanceOf(Map);
    expect(map.get(1)).toEqual({ name: "Ada" });
    await expect(env.CORE.sumNumberMap(new Map([[1, 2], [3, 4]]))).resolves.toBe(6);
    await expect(env.CORE.hasCompositeSet(new Set([{ id: 7 }]))).resolves.toBe(true);
    expect(await env.CORE.buildNumberTotals()).toEqual(new Map([[1, 2], [3, 4]]));
    expect(await env.CORE.buildTupleSet()).toEqual(new Set([[7, 8]]));
    const cyclic = new Map();
    cyclic.set(1, cyclic);
    await expect(env.CORE.numberMapKeepsCycle(cyclic)).resolves.toBe(true);
    const response = await invoke("/rpc-key-collections");
    expect(await response.json()).toEqual({ sum: 6, contains: true, description: "user:Ada" });
  });

  it("restores cyclic typed results through InvokeAsync", async () => {
    const response = await invoke("/rpc-typed-graph");
    expect(await response.text()).toBe("self");
  });

  it("keeps typed service bindings callable through Env.Get", async () => {
    const response = await invoke("/typed-service");
    expect(await response.json()).toEqual({ name: "Ada", description: "user:Ada", graph: "self" });
  });

  it("preserves the native collection kind behind readonly collection arguments", async () => {
    await expect(env.CORE.collectionSummary(new Set([1, 2]))).resolves.toEqual({ count: 2, total: 3, visits: 2 });
    await expect(env.CORE.collectionSummary([1, 2])).resolves.toEqual({ count: 2, total: 3, visits: 2 });
  });

  it("restores transported state without re-running constructors", async () => {
    await expect(env.CORE.describeCtorProbe({})).resolves.toBe("calls:0");
    const privateState = await env.CORE.buildPrivateState();
    await expect(env.CORE.describePrivateState(privateState)).resolves.toBe("user:Ada");
    const primaryState = await env.CORE.buildPrimaryState();
    await expect(env.CORE.describePrimaryState(primaryState)).resolves.toBe("user:Ada");
  });

  it("restores nested collection values while preserving aliases", async () => {
    const user = { name: "Ada" };
    await expect(env.CORE.describeCollectionData({
      array: [user], byName: { x: user }, pair: [user, user],
      sequence: [user], points: new Set([3, 4]),
    })).resolves.toBe(true);
    await expect(env.CORE.describeUserPairs({ ada: user })).resolves.toBe("user:Ada");
    await expect(env.CORE.describeQueue([user])).resolves.toBe("user:Ada");
    await expect(env.CORE.describeStack([user])).resolves.toBe("user:Ada");
    await expect(env.CORE.describeSetInterface(new Set([user]))).resolves.toBe("user:Ada");
    await expect(env.CORE.describeReadOnlySet(new Set([user]))).resolves.toBe("user:Ada");
  });

  it("restores typed RPC argument behavior without losing graph identity", async () => {
    const user = { name: "Ada" };
    const graph = {};
    graph.next = graph;
    await expect(env.CORE.describeIncoming(user)).resolves.toBe("user:Ada");
    await expect(env.CORE.describeBuiltUser()).resolves.toBe("user:Ada");
    await expect(env.CORE.incomingUserKeepsIdentity(user, user)).resolves.toBe(true);
    await expect(env.CORE.describeIncomingGraph(graph)).resolves.toBe("self");
    await expect(env.CORE.describeIncomingUsers({ users: [user] })).resolves.toBe("user:Ada");
    const derived = { name: "base", user };
    await expect(env.CORE.hasTypedAliases(derived, derived)).resolves.toBe(true);
    await expect(env.CORE.hasReversedTypedAliases(derived, derived)).resolves.toBe(true);
    const items = [derived];
    await expect(env.CORE.hasTypedArrayAliases(items, items)).resolves.toBe(true);
    await expect(env.CORE.hasReversedTypedArrayAliases(items, items)).resolves.toBe(true);
  });

  it("keeps C# class behavior and identity for internal calls to public RPC methods", async () => {
    await expect(env.CORE.usePublicUser()).resolves.toBe("user:Ada");
    await expect(env.CORE.useUserThroughInterface()).resolves.toBe("user:Ada");
    await expect(env.CORE.implementsCoreInterface()).resolves.toBe(true);
    await expect(env.CORE.useDefaultInterface()).resolves.toBe("user:Ada");
    await expect(env.CORE.useDefaultProperty()).resolves.toBe("user:Ada");
    await expect(env.CORE.useInterfaceDelegate()).resolves.toBe("user:Ada");
    await expect(env.CORE.useGenericInterface()).resolves.toBe(15);
    await expect(env.CORE.useMixedInterfaces()).resolves.toBe("user:Ada:user:Local");
    await expect(env.CORE.useCovariantInterface()).resolves.toBe(true);
    await expect(env.CORE.useAwaitedUser()).resolves.toBe("user:Ada");
    await expect(env.CORE.publicUserKeepsIdentity()).resolves.toBe(true);
    await expect(env.CORE.explicitUserKeepsIdentity()).resolves.toBe(true);
    await expect(env.CORE.useUserFromHelper()).resolves.toBe("user:Ada");
    await expect(env.CORE.buildUser()).resolves.toEqual({ name: "Ada" });
  });

  it("converts user classes returned through object to native RPC values", async () => {
    await expect(env.CORE.buildBoxedUser()).resolves.toEqual({ name: "Ada" });
    await expect(env.CORE.buildInterfaceUser()).resolves.toEqual({ name: "Ada" });
    const users = await env.CORE.buildBoxedUsers();
    expect(users).toEqual([{ name: "Ada" }, { name: "Ada" }]);
    expect(users[0]).toBe(users[1]);
  });

  it("applies optional parameters to both internal and exported calls", async () => {
    await expect(env.CORE.useDefaultUser()).resolves.toBe("user:Grace");
    await expect(env.CORE.buildDefaultUser()).resolves.toEqual({ name: "Grace" });
    await expect(env.CORE.buildDefaultUser("Lin")).resolves.toEqual({ name: "Lin" });
  });

  it("supports cycles and shared references through native RPC", async () => {
    const graph = {};
    graph.next = graph;
    const array = [];
    array.push(array);
    await expect(env.CORE.hasCycle(graph)).resolves.toBe(true);
    await expect(env.CORE.hasArrayCycle(array)).resolves.toBe(true);
    await expect(env.CORE.sameReference(graph, graph)).resolves.toBe(true);
  });

  it("preserves cycles and shared references in compiled RPC arguments", async () => {
    const response = await invoke("/rpc-graphs");
    await expect(response.json()).resolves.toEqual({ cycle: true, arrayCycle: true, shared: true });
  });

  it("runs awaited switch arms and conditional calls inside generated methods", async () => {
    await expect(env.CORE.asyncControlFlow(0)).resolves.toBe("awaited");
    await expect(env.CORE.asyncControlFlow(1)).resolves.toBe("present");
    await expect(env.CORE.asyncControlFlow(-1)).resolves.toBe("missing");
  });

  it("restarts async iterator helpers with their original arguments and current receiver", async () => {
    await expect(env.CORE.iteratorSnapshot(2)).resolves.toEqual([12, 13, 22, 23]);
  });

  it("calls the same service through RPC and fetch", async () => {
    const response = await invoke("/health");
    const result = await response.json();
    expect(result.rpc).toMatchObject({ ok: true, service: "core" });
    expect(result.http).toMatchObject({ ok: true, service: "core" });
  });

  it("propagates RPC values, defaults, D1 work, and validation results", async () => {
    const created = await invoke("/users", { method: "POST", body: JSON.stringify({ username: "ada_1" }) });
    expect(created.status).toBe(201);
    const user = await created.json();
    expect(user.username).toBe("ada_1");

    const list = await (await invoke("/users?prefix=ada")).json();
    expect(list.users).toHaveLength(1);
    expect((await invoke(`/users/${user.id}`)).status).toBe(200);

    const invalid = await invoke("/users", { method: "POST", body: JSON.stringify({ username: "!" }) });
    expect(invalid.status).toBe(400);
    await expect(invalid.json()).resolves.toEqual({ error: "Invalid username" });
  });

  it("returns a native Response through named RPC", async () => {
    const response = await invoke("/assets/missing.txt");
    expect(response.status).toBe(404);
    expect(response.headers.get("x-served-via")).toBe("rpc");
  });
});
