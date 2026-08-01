"use strict";

const fs = require("node:fs");
const path = require("node:path");
const { spawnSync } = require("node:child_process");

function realpathOrNull(candidate) {
  try {
    return fs.realpathSync(candidate);
  } catch {
    return null;
  }
}

function collectSelfRealpaths(entryScriptPath) {
  const self = new Set();
  const add = (candidate) => {
    const real = realpathOrNull(candidate);
    if (real) {
      self.add(real);
    }
  };

  add(entryScriptPath);
  add(__filename);

  // Cover both the package bin files and any install shims that point at them.
  add(path.join(__dirname, "..", "bin", "npm.js"));
  add(path.join(__dirname, "..", "bin", "npx.js"));

  return self;
}

function candidateNames(command) {
  if (process.platform !== "win32") {
    return [command];
  }

  const exts = (process.env.PATHEXT || ".EXE;.CMD;.BAT;.COM")
    .split(";")
    .filter(Boolean);
  const hasExt = exts.some((ext) => command.toLowerCase().endsWith(ext.toLowerCase()));
  if (hasExt) {
    return [command];
  }

  return [command, ...exts.map((ext) => command + ext)];
}

function findExternalCommand(command, selfRealpaths) {
  const pathParts = (process.env.PATH || "").split(path.delimiter).filter(Boolean);

  for (const dir of pathParts) {
    for (const name of candidateNames(command)) {
      const candidate = path.join(dir, name);
      const real = realpathOrNull(candidate);
      if (!real || selfRealpaths.has(real)) {
        continue;
      }

      try {
        fs.accessSync(real, fs.constants.X_OK);
      } catch {
        // Not executable; keep looking.
        continue;
      }

      return real;
    }
  }

  return null;
}

function forward(command, entryScriptPath = process.argv[1]) {
  const selfRealpaths = collectSelfRealpaths(entryScriptPath);
  const target = findExternalCommand(command, selfRealpaths);

  if (!target) {
    console.error(`eds-npm-cli-stub: could not find a system ${command} on PATH`);
    process.exit(1);
  }

  const result = spawnSync(target, process.argv.slice(2), {
    stdio: "inherit",
    env: process.env,
    shell: false,
  });

  if (result.error) {
    console.error(result.error.message);
    process.exit(1);
  }

  process.exit(result.status == null ? 1 : result.status);
}

module.exports = { forward };
