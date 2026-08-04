"use strict";

const { describe, it, before, after } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { spawnSync } = require("node:child_process");

const {
  extractJsRefs,
  shimTargetsSelf,
  collectSelfRealpaths,
  findExternalCommand,
  STUB_ACTIVE_ENV,
} = require("../lib/forward.js");

describe("extractJsRefs", () => {
  it("captures quoted .js paths that contain spaces", () => {
    const dir = String.raw`C:\Users\John Doe\repo\node_modules\.bin`;
    const expanded =
      String.raw`"%_prog%"  "` + dir + String.raw`\..\eds-npm-cli-stub\bin\npm.js" %*`;
    const refs = extractJsRefs(expanded, dir);
    assert.ok(
      refs.some((r) => r.includes("John Doe") && r.endsWith("npm.js")),
      `expected spaced path in refs, got: ${JSON.stringify(refs)}`
    );
  });

  it("captures unquoted dir-prefixed .js paths that contain spaces", () => {
    const dir = "/Users/John Doe/repo/node_modules/.bin";
    const expanded = `exec node ${dir}/../eds-npm-cli-stub/bin/npm.js "$@"`;
    const refs = extractJsRefs(expanded, dir);
    assert.ok(
      refs.some((r) => r.includes("John Doe") && r.endsWith("npm.js")),
      `expected spaced path in refs, got: ${JSON.stringify(refs)}`
    );
  });

  it("still captures plain unquoted refs without spaces", () => {
    const dir = "/repo/node_modules/.bin";
    const expanded = 'exec node ../eds-npm-cli-stub/bin/npm.js "$@"';
    const refs = extractJsRefs(expanded, dir);
    assert.ok(refs.includes("../eds-npm-cli-stub/bin/npm.js"));
  });

  it("does not treat .json as a .js reference", () => {
    const dir = "/repo/.bin";
    const expanded = `${dir}/package.json and "${dir}/other.json"`;
    const refs = extractJsRefs(expanded, dir);
    assert.deepEqual(refs, []);
  });
});

describe("shimTargetsSelf with spaced install path", () => {
  let root;
  let binDir;
  let stubJs;
  let selfRealpaths;

  before(() => {
    root = fs.mkdtempSync(path.join(os.tmpdir(), "npm-stub spaced-"));
    binDir = path.join(root, "node_modules", ".bin");
    const stubPkg = path.join(root, "node_modules", "eds-npm-cli-stub", "bin");
    fs.mkdirSync(binDir, { recursive: true });
    fs.mkdirSync(stubPkg, { recursive: true });
    stubJs = path.join(stubPkg, "npm.js");
    fs.writeFileSync(stubJs, '#!/usr/bin/env node\nconsole.log("stub");\n');

    // Mimic a Windows cmd-shim that points at our stub via %dp0%.
    const cmdShim = `@ECHO off
SETLOCAL
SET dp0=%~dp0
"%_prog%"  "%dp0%\\..\\eds-npm-cli-stub\\bin\\npm.js" %*
`;
    fs.writeFileSync(path.join(binDir, "npm.cmd"), cmdShim);

    // Mimic a Git Bash shim that points at our stub via $basedir.
    const bashShim = `#!/bin/sh
basedir=$(dirname "$0")
exec node  "$basedir/../eds-npm-cli-stub/bin/npm.js" "$@"
`;
    fs.writeFileSync(path.join(binDir, "npm"), bashShim);

    selfRealpaths = new Set([fs.realpathSync(stubJs)]);
  });

  after(() => {
    fs.rmSync(root, { recursive: true, force: true });
  });

  it("detects cmd-shim targeting self when path has spaces", () => {
    const shim = path.join(binDir, "npm.cmd");
    assert.equal(shimTargetsSelf(shim, selfRealpaths), true);
  });

  it("detects Git Bash shim targeting self when path has spaces", () => {
    const shim = path.join(binDir, "npm");
    assert.equal(shimTargetsSelf(shim, selfRealpaths), true);
  });

  it("does not treat an unrelated shim as self", () => {
    const other = path.join(binDir, "other.cmd");
    fs.writeFileSync(
      other,
      '@ECHO off\n"%_prog%"  "%dp0%\\..\\some-other-pkg\\bin\\cli.js" %*\n'
    );
    assert.equal(shimTargetsSelf(other, selfRealpaths), false);
  });

  it("skips self-shims when finding an external command", () => {
    const externalDir = path.join(root, "external-bin");
    fs.mkdirSync(externalDir, { recursive: true });
    const external = path.join(externalDir, "npm");
    fs.writeFileSync(external, "#!/bin/sh\necho external\n");
    fs.chmodSync(external, 0o755);

    const prevPath = process.env.PATH;
    process.env.PATH = [binDir, externalDir].join(path.delimiter);
    try {
      const found = findExternalCommand("npm", selfRealpaths);
      assert.equal(found, fs.realpathSync(external));
    } finally {
      process.env.PATH = prevPath;
    }
  });
});

describe("recursion guard", () => {
  it("fails fast when EDS_NPM_STUB_ACTIVE is already set", () => {
    const npmBin = path.join(__dirname, "..", "bin", "npm.js");
    const result = spawnSync(process.execPath, [npmBin, "--version"], {
      encoding: "utf8",
      env: { ...process.env, [STUB_ACTIVE_ENV]: "1", PATH: process.env.PATH },
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /refused to re-enter/);
    assert.match(result.stderr, new RegExp(STUB_ACTIVE_ENV));
  });
});

describe("collectSelfRealpaths", () => {
  it("includes package bin entrypoints", () => {
    const entry = path.join(__dirname, "..", "bin", "npm.js");
    const self = collectSelfRealpaths(entry);
    assert.ok(self.has(fs.realpathSync(entry)));
    assert.ok(
      self.has(fs.realpathSync(path.join(__dirname, "..", "bin", "npx.js")))
    );
  });
});
