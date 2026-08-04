"use strict";

const fs = require("node:fs");
const path = require("node:path");
const { spawnSync } = require("node:child_process");

/**
 * Marker set on the forwarded child so an immediate self-shim re-entry fails
 * fast. Nested npm/npx from lifecycle scripts inherit it too, so the guard
 * must ignore those (see isImmediateReentry).
 */
const STUB_ACTIVE_ENV = "EDS_NPM_STUB_ACTIVE";

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

/**
 * Extract .js path references from expanded shim text.
 * Quoted paths and paths that start with the shim directory may contain spaces
 * (common when the Windows user profile or checkout path has whitespace).
 */
function extractJsRefs(expanded, dir) {
  const refs = [];
  const seen = new Set();
  const add = (ref) => {
    if (!ref || seen.has(ref)) {
      return;
    }
    seen.add(ref);
    refs.push(ref);
  };

  // Quoted segments: "…npm.js" / '…npm.js'
  const quotedRe = /(["'])([^"'<>|\r\n]*?\.js)\1/gi;
  let match;
  while ((match = quotedRe.exec(expanded)) !== null) {
    add(match[2]);
  }

  // Unquoted refs anchored on the expanded shim directory (may contain spaces).
  let from = 0;
  while (from < expanded.length) {
    const idx = expanded.indexOf(dir, from);
    if (idx === -1) {
      break;
    }
    const jsIdx = expanded.indexOf(".js", idx + dir.length);
    if (jsIdx === -1) {
      from = idx + 1;
      continue;
    }
    const after = expanded[jsIdx + 3];
    if (after && /[A-Za-z0-9_]/.test(after)) {
      // e.g. ".json" — keep scanning
      from = idx + 1;
      continue;
    }
    const candidate = expanded.slice(idx, jsIdx + 3);
    // Stop at shell/path delimiters that cannot appear in a file path ref.
    if (/[\r\n"'<>|]/.test(candidate.slice(dir.length))) {
      from = idx + 1;
      continue;
    }
    add(candidate);
    from = jsIdx + 3;
  }

  // Fallback: unquoted refs without whitespace (relative paths, no dir prefix).
  const plain = expanded.match(/[^\s"'<>|]+\.js\b/g) || [];
  for (const ref of plain) {
    add(ref);
  }

  return refs;
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
  const refs = extractJsRefs(expanded, dir);
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

/**
 * True only for immediate self-shim re-entry, not for nested npm/npx invoked
 * from lifecycle scripts (which inherit EDS_NPM_STUB_ACTIVE from the forwarded
 * real npm and also set npm_lifecycle_event / npm_command).
 */
function isImmediateReentry() {
  if (!process.env[STUB_ACTIVE_ENV]) {
    return false;
  }
  if (process.env.npm_lifecycle_event || process.env.npm_command) {
    return false;
  }
  return true;
}

function isWinShellWrapper(target) {
  return /\.(?:cmd|bat)$/i.test(target);
}

/**
 * Escape one argv token for cmd.exe when Node will concatenate
 * `file + ' ' + args.join(' ')` under `spawn({ shell: true })`.
 *
 * Double-quote the token, double embedded quotes, and neutralize `%` / `!`
 * so env / delayed-expansion cannot rewrite the argument.
 */
function escapeArgForCmd(arg) {
  const s = String(arg).replace(/%/g, "%%").replace(/!/g, "^!").replace(/"/g, '""');
  return `"${s}"`;
}

function resolveNodeNearWrapper(wrapperDir) {
  const sibling = path.join(wrapperDir, "node.exe");
  try {
    if (fs.existsSync(sibling)) {
      return sibling;
    }
  } catch {
    // fall through
  }
  // Match official npm.cmd / npx.cmd: when %~dp0\node.exe is absent, use
  // PATH `node` — not the Node running this stub (which may differ).
  return "node";
}

/**
 * Mirror official npm.cmd / npx.cmd: run npm-prefix.js and, when a CLI exists
 * under that prefix, prefer it over the bundled default path.
 *
 * @returns {string | null}
 */
function resolveCliViaNpmPrefix(nodeExe, prefixJs, preferName) {
  try {
    const result = spawnSync(nodeExe, [prefixJs], {
      encoding: "utf8",
      windowsHide: true,
      shell: false,
    });
    if (result.error || result.status !== 0) {
      return null;
    }
    const lines = String(result.stdout || "")
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(Boolean);
    const prefix = lines.length > 0 ? lines[lines.length - 1] : "";
    if (!prefix) {
      return null;
    }
    const candidate = path.join(
      prefix,
      "node_modules",
      "npm",
      "bin",
      preferName
    );
    if (!fs.existsSync(candidate)) {
      return null;
    }
    return realpathOrNull(candidate) || candidate;
  } catch {
    return null;
  }
}

/**
 * Unwrap a Windows .cmd/.bat shim to `node <cli.js>` so we can spawn with
 * `shell: false` and an intact argv array (avoids cmd metacharacter injection
 * and space-splitting from Node's unescaped shell join).
 *
 * @returns {{ command: string, args: string[] } | null}
 */
function resolveWinShellWrapper(target, command = "npm") {
  if (!isWinShellWrapper(target)) {
    return null;
  }

  let content;
  try {
    const st = fs.statSync(target);
    if (!st.isFile() || st.size > 65536) {
      return null;
    }
    content = fs.readFileSync(target, "utf8");
  } catch {
    return null;
  }
  if (content.includes("\0")) {
    return null;
  }

  const dir = path.dirname(target);
  // %~dp0 includes a trailing separator; keep that so concatenated relative
  // segments in official Node/npm wrappers resolve correctly.
  const dp0 = dir.endsWith("\\") || dir.endsWith("/") ? dir : dir + path.sep;
  const expanded = content
    .replace(/%~dp0/gi, dp0)
    .replace(/%dp0%/gi, dp0)
    .replace(/\$basedir/g, dir);

  const refs = extractJsRefs(expanded, dir);
  const resolved = [];
  const seen = new Set();
  for (const ref of refs) {
    const real = realpathOrNull(path.resolve(dir, ref.replace(/\\/g, "/")));
    if (!real || seen.has(real) || !real.toLowerCase().endsWith(".js")) {
      continue;
    }
    seen.add(real);
    resolved.push(real);
  }
  if (resolved.length === 0) {
    return null;
  }

  const preferName =
    String(command).toLowerCase() === "npx" ? "npx-cli.js" : "npm-cli.js";
  const preferred =
    resolved.find((p) => path.basename(p).toLowerCase() === preferName) ||
    resolved.find((p) => {
      const base = path.basename(p).toLowerCase();
      return base.endsWith("-cli.js") && !base.includes("prefix");
    }) ||
    resolved.find((p) => !path.basename(p).toLowerCase().includes("prefix"));

  if (!preferred) {
    return null;
  }

  const nodeExe = resolveNodeNearWrapper(dir);
  const prefixJs = resolved.find(
    (p) => path.basename(p).toLowerCase() === "npm-prefix.js"
  );
  const cli =
    (prefixJs && resolveCliViaNpmPrefix(nodeExe, prefixJs, preferName)) ||
    preferred;

  return {
    command: nodeExe,
    args: [cli],
  };
}

/**
 * Build the spawnSync file/args/shell triple for forwarding.
 * Prefer unwrapping .cmd/.bat to node+js; fall back to shell:true with
 * cmd-escaped arguments when unwrapping is not possible.
 */
function buildSpawnInvocation(target, forwardedArgs, command = "npm") {
  const args = forwardedArgs.map(String);

  if (!isWinShellWrapper(target)) {
    return { file: target, args, shell: false };
  }

  const unwrapped = resolveWinShellWrapper(target, command);
  if (unwrapped) {
    return {
      file: unwrapped.command,
      args: [...unwrapped.args, ...args],
      shell: false,
    };
  }

  // Node rejects direct .cmd/.bat spawns without a shell (EINVAL / CVE-2024-27980).
  // Pre-escape every token because Node joins the array with spaces unescaped.
  return {
    file: escapeArgForCmd(target),
    args: args.map(escapeArgForCmd),
    shell: true,
  };
}

function forward(command, entryScriptPath = process.argv[1]) {
  if (isImmediateReentry()) {
    console.error(
      `eds-npm-cli-stub: refused to re-enter while forwarding ${command} ` +
        `(${STUB_ACTIVE_ENV} is set). Self-shim detection likely failed; ` +
        `check that the install path is handled correctly.`
    );
    process.exit(1);
  }

  const selfRealpaths = collectSelfRealpaths(entryScriptPath);
  const target = findExternalCommand(command, selfRealpaths);

  if (!target) {
    console.error(`eds-npm-cli-stub: could not find a system ${command} on PATH`);
    process.exit(1);
  }

  const invocation = buildSpawnInvocation(target, process.argv.slice(2), command);
  const env = { ...process.env, [STUB_ACTIVE_ENV]: "1" };
  const result = spawnSync(invocation.file, invocation.args, {
    stdio: "inherit",
    env,
    shell: invocation.shell,
  });

  if (result.error) {
    console.error(result.error.message);
    process.exit(1);
  }

  process.exit(result.status == null ? 1 : result.status);
}

module.exports = {
  forward,
  extractJsRefs,
  shimTargetsSelf,
  collectSelfRealpaths,
  findExternalCommand,
  isImmediateReentry,
  escapeArgForCmd,
  resolveWinShellWrapper,
  buildSpawnInvocation,
  STUB_ACTIVE_ENV,
};
