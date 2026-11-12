import test from "node:test";
import assert from "node:assert/strict";
import { isHostUnregistered } from "../nativePort.js";

test("isHostUnregistered: only the 'not found' error means uninstalled", () => {
    assert.equal(isHostUnregistered("Specified native messaging host not found."), true);
    assert.equal(isHostUnregistered("Failed to start native messaging host."), false);
    assert.equal(isHostUnregistered("Native host has exited."), false);
    assert.equal(isHostUnregistered("Access to the specified native messaging host is forbidden."), false);
    assert.equal(isHostUnregistered("host disconnected"), false);
    assert.equal(isHostUnregistered(undefined), false);
});
