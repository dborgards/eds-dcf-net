#!/usr/bin/env node
"use strict";

const { spawnSync } = require("node:child_process");
const path = require("node:path");

const stubDir = __dirname;
const filteredPath = (process.env.PATH || "")
  .split(path.delimiter)
  .filter((part) => part && path.resolve(part) !== path.resolve(stubDir))
  .join(path.delimiter);

const result = spawnSync("npx", process.argv.slice(2), {
  stdio: "inherit",
  env: { ...process.env, PATH: filteredPath },
  shell: process.platform === "win32",
});

if (result.error) {
  console.error(result.error.message);
  process.exit(1);
}

process.exit(result.status == null ? 1 : result.status);
