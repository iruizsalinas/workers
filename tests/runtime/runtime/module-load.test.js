import { describe, expect, it } from "vitest";
import * as batchAccumulator from "../fixtures/BatchAccumulator/dist/worker.js";
import * as bodyPipeline from "../fixtures/BodyPipeline/dist/worker.js";
import * as chatRoom from "../fixtures/ChatRoom/dist/worker.js";
import * as compilerSemantics from "../fixtures/CompilerSemantics/dist/worker.js";
import * as edgeMiddleware from "../fixtures/EdgeMiddleware/dist/worker.js";
import * as eventWrappers from "../fixtures/EventWrappers/dist/worker.js";
import * as fileGateway from "../fixtures/FileGateway/dist/worker.js";
import * as htmlProxy from "../fixtures/HtmlProxy/dist/worker.js";
import * as jobProcessor from "../fixtures/JobProcessor/dist/worker.js";
import * as mockedBindings from "../fixtures/MockBindings/dist/worker.js";
import * as nativeBindings from "../fixtures/NativeBindings/dist/worker.js";
import * as runtimeIntrinsics from "../fixtures/RuntimeIntrinsics/dist/worker.js";
import * as serviceGateway from "../fixtures/ServiceGateway/dist/worker.js";
import * as signedGateway from "../fixtures/SignedGateway/dist/worker.js";
import * as userApi from "../fixtures/UserApi/dist/worker.js";

const fixtures = [
  batchAccumulator, bodyPipeline, chatRoom, compilerSemantics, edgeMiddleware,
  eventWrappers, fileGateway, htmlProxy, jobProcessor, mockedBindings,
  nativeBindings, runtimeIntrinsics, serviceGateway, signedGateway, userApi,
];

describe("generated Worker modules", () => {
  it("loads every successful fixture as an ES module in workerd", () => {
    for (const fixture of fixtures)
      expect(Object.keys(fixture).length).toBeGreaterThan(0);
  });
});
