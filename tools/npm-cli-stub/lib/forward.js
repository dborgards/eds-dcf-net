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

  // Package bin entrypoints (Unix .bin symlinks share these realpaths).
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

  // Prefer PATHEXT wrappers (npm.cmd) over Node's extensionless shell shims.
  // CreateProcess cannot launch those shims without a shell, so listing the
  // bare name first makes Windows forwarding fail even when npm.cmd exists.
  return [...exts.map((ext) => command + ext), command];
}

// Windows cmd-shim / Git Bash wrappers are separate files (not symlinks) that
// invoke node with a path to our bin script. Their realpath differs from the
// .js entry, so detect them by resolving .js references in the shim text.
function shimTargetsSelf(candidatePath, selfRealpaths) {
  let content;
  try {
    const st = fs.statSync(candidatePath);
    if (!st.isFile() || st.size > 16384) {
      return false;
    }
    content = fs.readFileSync(candidatePath, "utf8");
  } catch {
    return false;
  }
  if (content.includes("\0")) {
    return false;
  }

  const dir = path.dirname(candidatePath);
  // Expand both modern (`%dp0%` after SET dp0=%~dp0) and older (`%~dp0\...`)
  // cmd-shim forms. Requiring a trailing % misses bare %~dp0 and can re-enter
  // the stub when node_modules/.bin is first on PATH.
  const expanded = content
    .replace(/%~dp0/gi, dir)
    .replace(/%dp0%/gi, dir)
    .replace(/\$basedir/g, dir);
  const refs = expanded.match(/[^\s"'<>|]+\.js\b/g) || [];
  for (const ref of refs) {
    const resolved = realpathOrNull(path.resolve(dir, ref.replace(/\\/g, "/")));
    if (resolved && selfRealpaths.has(resolved)) {
      return true;
    }
  }
  return false;
}

function findExternalCommand(command, selfRealpaths) {
  const pathParts = (process.env.PATH || "").split(path.delimiter).filter(Boolean);

  for (const dir of pathParts) {
    for (const name of candidateNames(command)) {
      const candidate = path.join(dir, name);
      const real = realpathOrNull(candidate);
      if (!real || selfRealpaths.has(real) || shimTargetsSelf(real, selfRealpaths)) {
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

  // Node rejects direct .cmd/.bat spawns without a shell (EINVAL / CVE-2024-27980).
  const shell =
    process.platform === "win32" && /\.(?:cmd|bat)$/i.test(target);

  const result = spawnSync(target, process.argv.slice(2), {
    stdio: "inherit",
    env: process.env,
    shell,
  });

  if (result.error) {
    console.error(result.error.message);
    process.exit(1);
  }

  process.exit(result.status == null ? 1 : result.status);
}

module.exports = { forward };
