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
  isImmediateReentry,
  escapeArgForCmd,
  resolveWinShellWrapper,
  buildSpawnInvocation,
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
  function withEnv(overrides, fn) {
    const keys = Object.keys(overrides);
    const previous = {};
    for (const key of keys) {
      previous[key] = process.env[key];
      if (overrides[key] === undefined) {
        delete process.env[key];
      } else {
        process.env[key] = overrides[key];
      }
    }
    try {
      return fn();
    } finally {
      for (const key of keys) {
        if (previous[key] === undefined) {
          delete process.env[key];
        } else {
          process.env[key] = previous[key];
        }
      }
    }
  }

  it("treats bare EDS_NPM_STUB_ACTIVE as immediate re-entry", () => {
    withEnv(
      {
        [STUB_ACTIVE_ENV]: "1",
        npm_lifecycle_event: undefined,
        npm_command: undefined,
      },
      () => {
        assert.equal(isImmediateReentry(), true);
      }
    );
  });

  it("allows nested npm during lifecycle scripts despite the marker", () => {
    withEnv(
      {
        [STUB_ACTIVE_ENV]: "1",
        npm_lifecycle_event: "build",
        npm_command: undefined,
      },
      () => {
        assert.equal(isImmediateReentry(), false);
      }
    );
  });

  it("fails fast on immediate re-entry via bin entrypoint", () => {
    const npmBin = path.join(__dirname, "..", "bin", "npm.js");
    const env = { ...process.env, [STUB_ACTIVE_ENV]: "1", PATH: process.env.PATH };
    delete env.npm_lifecycle_event;
    delete env.npm_command;
    const result = spawnSync(process.execPath, [npmBin, "--version"], {
      encoding: "utf8",
      env,
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /refused to re-enter/);
    assert.match(result.stderr, new RegExp(STUB_ACTIVE_ENV));
  });

  it("does not refuse re-entry when npm_command is set (nested npm)", () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "npm-stub-nested-"));
    try {
      const externalDir = path.join(root, "bin");
      fs.mkdirSync(externalDir, { recursive: true });
      const external = path.join(externalDir, "npm");
      fs.writeFileSync(external, "#!/bin/sh\necho nested-ok\n");
      fs.chmodSync(external, 0o755);

      const npmBin = path.join(__dirname, "..", "bin", "npm.js");
      const result = spawnSync(process.execPath, [npmBin, "--version"], {
        encoding: "utf8",
        env: {
          ...process.env,
          [STUB_ACTIVE_ENV]: "1",
          npm_command: "run-script",
          PATH: externalDir,
        },
      });
      assert.equal(result.status, 0, result.stderr);
      assert.match(result.stdout, /nested-ok/);
    } finally {
      fs.rmSync(root, { recursive: true, force: true });
    }
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

describe("escapeArgForCmd", () => {
  it("wraps arguments in double quotes", () => {
    assert.equal(escapeArgForCmd("a b"), '"a b"');
  });

  it("doubles embedded quotes and neutralizes % and !", () => {
    assert.equal(escapeArgForCmd('x"y'), '"x""y"');
    assert.equal(escapeArgForCmd("x%PATH%y"), '"x%%PATH%%y"');
    assert.equal(escapeArgForCmd("a!b"), '"a^!b"');
  });

  it("preserves shell metacharacters inside the quoted token", () => {
    assert.equal(escapeArgForCmd("x&y"), '"x&y"');
    assert.equal(escapeArgForCmd("a|b>c"), '"a|b>c"');
  });
});

describe("resolveWinShellWrapper / buildSpawnInvocation", () => {
  let root;

  before(() => {
    root = fs.mkdtempSync(path.join(os.tmpdir(), "npm-stub-unwrap-"));
  });

  after(() => {
    fs.rmSync(root, { recursive: true, force: true });
  });

  it("unwraps an official-style npm.cmd to node + npm-cli.js without a shell", () => {
    const prefix = path.join(root, "Program Files", "nodejs");
    const npmBin = path.join(prefix, "node_modules", "npm", "bin");
    fs.mkdirSync(npmBin, { recursive: true });
    const npmCli = path.join(npmBin, "npm-cli.js");
    const npmPrefix = path.join(npmBin, "npm-prefix.js");
    fs.writeFileSync(npmCli, "#!/usr/bin/env node\n");
    // Empty stdout → keep bundled CLI (same as IF EXIST failing in the batch).
    fs.writeFileSync(npmPrefix, "#!/usr/bin/env node\n");

    const npmCmd = path.join(prefix, "npm.cmd");
    fs.writeFileSync(
      npmCmd,
      `@ECHO OFF
SETLOCAL
SET "NODE_EXE=%~dp0\\node.exe"
IF NOT EXIST "%NODE_EXE%" (
  SET "NODE_EXE=node"
)
SET "NPM_PREFIX_JS=%~dp0\\node_modules\\npm\\bin\\npm-prefix.js"
SET "NPM_CLI_JS=%~dp0\\node_modules\\npm\\bin\\npm-cli.js"
FOR /F "delims=" %%F IN ('CALL "%NODE_EXE%" "%NPM_PREFIX_JS%"') DO (
  SET "NPM_PREFIX_NPM_CLI_JS=%%F\\node_modules\\npm\\bin\\npm-cli.js"
)
IF EXIST "%NPM_PREFIX_NPM_CLI_JS%" (
  SET "NPM_CLI_JS=%NPM_PREFIX_NPM_CLI_JS%"
)
"%NODE_EXE%" "%NPM_CLI_JS%" %*
`
    );

    const unwrapped = resolveWinShellWrapper(npmCmd, "npm");
    assert.ok(unwrapped, "expected unwrap to succeed");
    assert.equal(unwrapped.command, "node");
    assert.equal(unwrapped.args.length, 1);
    assert.equal(fs.realpathSync(unwrapped.args[0]), fs.realpathSync(npmCli));

    const spaced = ["run", "foo", "--", "a b", "x&y"];
    const invocation = buildSpawnInvocation(npmCmd, spaced, "npm");
    assert.equal(invocation.shell, false);
    assert.equal(invocation.file, "node");
    assert.deepEqual(invocation.args.slice(-spaced.length), spaced);
    assert.equal(
      fs.realpathSync(invocation.args[0]),
      fs.realpathSync(npmCli)
    );
  });

  it("applies npm-prefix.js override when that CLI exists", () => {
    const bundled = path.join(root, "bundled-node");
    const npmBin = path.join(bundled, "node_modules", "npm", "bin");
    fs.mkdirSync(npmBin, { recursive: true });
    const bundledCli = path.join(npmBin, "npm-cli.js");
    const npmPrefix = path.join(npmBin, "npm-prefix.js");
    fs.writeFileSync(bundledCli, "#!/usr/bin/env node\n");

    const upgraded = path.join(root, "upgraded-prefix");
    const upgradedBin = path.join(upgraded, "node_modules", "npm", "bin");
    fs.mkdirSync(upgradedBin, { recursive: true });
    const upgradedCli = path.join(upgradedBin, "npm-cli.js");
    fs.writeFileSync(upgradedCli, "#!/usr/bin/env node\n");

    fs.writeFileSync(
      npmPrefix,
      `#!/usr/bin/env node
process.stdout.write(${JSON.stringify(upgraded)});
`
    );

    const npmCmd = path.join(bundled, "npm.cmd");
    fs.writeFileSync(
      npmCmd,
      `@ECHO OFF
SET "NPM_PREFIX_JS=%~dp0\\node_modules\\npm\\bin\\npm-prefix.js"
SET "NPM_CLI_JS=%~dp0\\node_modules\\npm\\bin\\npm-cli.js"
"%NODE_EXE%" "%NPM_CLI_JS%" %*
`
    );

    const unwrapped = resolveWinShellWrapper(npmCmd, "npm");
    assert.ok(unwrapped);
    assert.equal(unwrapped.command, "node");
    assert.equal(
      fs.realpathSync(unwrapped.args[0]),
      fs.realpathSync(upgradedCli)
    );
  });

  it("uses sibling node.exe when present beside the wrapper", () => {
    const prefix = path.join(root, "with-sibling-node");
    const npmBin = path.join(prefix, "node_modules", "npm", "bin");
    fs.mkdirSync(npmBin, { recursive: true });
    const npmCli = path.join(npmBin, "npm-cli.js");
    fs.writeFileSync(npmCli, "#!/usr/bin/env node\n");
    const sibling = path.join(prefix, "node.exe");
    fs.writeFileSync(sibling, "not-a-real-exe\n");

    const npmCmd = path.join(prefix, "npm.cmd");
    fs.writeFileSync(
      npmCmd,
      `@ECHO OFF
SET "NPM_CLI_JS=%~dp0\\node_modules\\npm\\bin\\npm-cli.js"
"%NODE_EXE%" "%NPM_CLI_JS%" %*
`
    );

    const unwrapped = resolveWinShellWrapper(npmCmd, "npm");
    assert.ok(unwrapped);
    assert.equal(unwrapped.command, sibling);
    assert.equal(fs.realpathSync(unwrapped.args[0]), fs.realpathSync(npmCli));
  });

  it("prefers npx-cli.js when forwarding npx", () => {
    const prefix = path.join(root, "npx-prefix");
    const npmBin = path.join(prefix, "node_modules", "npm", "bin");
    fs.mkdirSync(npmBin, { recursive: true });
    const npxCli = path.join(npmBin, "npx-cli.js");
    const npmCli = path.join(npmBin, "npm-cli.js");
    fs.writeFileSync(npxCli, "#!/usr/bin/env node\n");
    fs.writeFileSync(npmCli, "#!/usr/bin/env node\n");

    const npxCmd = path.join(prefix, "npx.cmd");
    fs.writeFileSync(
      npxCmd,
      `@ECHO OFF
SET "NPX_CLI_JS=%~dp0\\node_modules\\npm\\bin\\npx-cli.js"
SET "NPM_CLI_JS=%~dp0\\node_modules\\npm\\bin\\npm-cli.js"
"%_prog%" "%NPX_CLI_JS%" %*
`
    );

    const unwrapped = resolveWinShellWrapper(npxCmd, "npx");
    assert.ok(unwrapped);
    assert.equal(fs.realpathSync(unwrapped.args[0]), fs.realpathSync(npxCli));
  });

  it("falls back to shell:true with escaped args on Windows when unwrap fails", () => {
    const opaque = path.join(root, "opaque.cmd");
    fs.writeFileSync(opaque, "@ECHO OFF\necho no js here\n");

    const args = ["run", "foo", "--", "a b", "x&y", "%PATH%"];
    const original = Object.getOwnPropertyDescriptor(process, "platform");
    Object.defineProperty(process, "platform", {
      configurable: true,
      value: "win32",
    });
    try {
      const invocation = buildSpawnInvocation(opaque, args, "npm");
      assert.equal(invocation.shell, true);
      assert.equal(invocation.file, escapeArgForCmd(opaque));
      assert.deepEqual(invocation.args, args.map(escapeArgForCmd));
    } finally {
      Object.defineProperty(process, "platform", original);
    }
  });

  it("does not use cmd shell fallback when unwrap fails off Windows", () => {
    const opaque = path.join(root, "opaque-posix.cmd");
    fs.writeFileSync(opaque, "@ECHO OFF\necho no js here\n");

    const args = ["run", "foo", "--", "a b", "x&y"];
    const original = Object.getOwnPropertyDescriptor(process, "platform");
    Object.defineProperty(process, "platform", {
      configurable: true,
      value: "linux",
    });
    try {
      const invocation = buildSpawnInvocation(opaque, args, "npm");
      assert.equal(invocation.shell, false);
      assert.equal(invocation.file, opaque);
      assert.deepEqual(invocation.args, args);
    } finally {
      Object.defineProperty(process, "platform", original);
    }
  });

  it("keeps shell:false for non-wrapper targets", () => {
    const unix = path.join(root, "npm");
    fs.writeFileSync(unix, "#!/bin/sh\n");
    const invocation = buildSpawnInvocation(unix, ["install", "a b"], "npm");
    assert.equal(invocation.shell, false);
    assert.equal(invocation.file, unix);
    assert.deepEqual(invocation.args, ["install", "a b"]);
  });

  it("forwards spaced and metacharacter args intact via unwrapped spawn", () => {
    const prefix = path.join(root, "echo-cli");
    const bin = path.join(prefix, "bin");
    fs.mkdirSync(bin, { recursive: true });
    const cliJs = path.join(bin, "npm-cli.js");
    // Print argv JSON so we can assert exact tokens after spawn.
    fs.writeFileSync(
      cliJs,
      `#!/usr/bin/env node
process.stdout.write(JSON.stringify(process.argv.slice(2)));
`
    );
    const npmCmd = path.join(prefix, "npm.cmd");
    fs.writeFileSync(
      npmCmd,
      `@ECHO OFF
node "%~dp0\\bin\\npm-cli.js" %*
`
    );

    const forwarded = ["run", "foo", "--", "a b", "x&y"];
    const invocation = buildSpawnInvocation(npmCmd, forwarded, "npm");
    assert.equal(invocation.shell, false);

    const result = spawnSync(invocation.file, invocation.args, {
      encoding: "utf8",
    });
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(JSON.parse(result.stdout), forwarded);
  });
});
