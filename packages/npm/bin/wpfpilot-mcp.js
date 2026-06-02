#!/usr/bin/env node

import { createReadStream, createWriteStream, existsSync, mkdirSync, rmSync } from "node:fs";
import { chmod, rename } from "node:fs/promises";
import { dirname, join } from "node:path";
import { spawn } from "node:child_process";
import { Readable } from "node:stream";
import { fileURLToPath } from "node:url";
import { tmpdir } from "node:os";
import { pipeline } from "node:stream/promises";
import unzipStream from "unzip-stream";

const { Extract } = unzipStream;

const REPO = process.env.WPFPILOT_REPO || "skuzadev/wpfpilot-mcp";
const VERSION = process.env.WPFPILOT_VERSION || "latest";
const ASSET = "wpfpilot-mcp-win-x64.zip";
const INSTALL_DIR =
  process.env.WPFPILOT_INSTALL_DIR ||
  join(process.env.LOCALAPPDATA || process.env.USERPROFILE || dirname(fileURLToPath(import.meta.url)), "WpfPilot", "npm");
const EXE = join(INSTALL_DIR, "wpfpilot-mcp.exe");

if (process.platform !== "win32") {
  console.error("WpfPilot MCP supports Windows only.");
  process.exit(1);
}

function apiUrl() {
  return VERSION === "latest"
    ? `https://api.github.com/repos/${REPO}/releases/latest`
    : `https://api.github.com/repos/${REPO}/releases/tags/${VERSION}`;
}

async function fetchJson(url) {
  const response = await fetch(url, {
    headers: {
      "User-Agent": "wpfpilot-mcp-npm"
    }
  });
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${response.statusText} (${url})`);
  }
  return response.json();
}

async function download(url, destination) {
  const response = await fetch(url, {
    headers: {
      "User-Agent": "wpfpilot-mcp-npm"
    }
  });
  if (!response.ok || !response.body) {
    throw new Error(`Download failed: ${response.status} ${response.statusText} (${url})`);
  }
  await pipeline(Readable.fromWeb(response.body), createWriteStream(destination));
}

async function ensureInstalled() {
  if (existsSync(EXE)) {
    return;
  }

  mkdirSync(INSTALL_DIR, { recursive: true });
  const release = await fetchJson(apiUrl());
  const asset = release.assets?.find((item) => item.name === ASSET);
  if (!asset) {
    throw new Error(`Release asset '${ASSET}' was not found in ${REPO} ${VERSION}.`);
  }

  const tempRoot = join(tmpdir(), `wpfpilot-${process.pid}-${Date.now()}`);
  const zipPath = join(tempRoot, ASSET);
  const extractDir = join(tempRoot, "extract");
  mkdirSync(extractDir, { recursive: true });

  try {
    await download(asset.browser_download_url, zipPath);
    await pipeline(createReadStream(zipPath), Extract({ path: extractDir }));
    rmSync(INSTALL_DIR, { recursive: true, force: true });
    mkdirSync(INSTALL_DIR, { recursive: true });
    await moveDirectoryContents(extractDir, INSTALL_DIR);
    if (!existsSync(EXE)) {
      throw new Error(`Install completed, but '${EXE}' was not found.`);
    }
    try {
      await chmod(EXE, 0o755);
    } catch {
      // Windows usually does not need chmod.
    }
  } finally {
    rmSync(tempRoot, { recursive: true, force: true });
  }
}

async function moveDirectoryContents(from, to) {
  const { readdir } = await import("node:fs/promises");
  for (const entry of await readdir(from)) {
    await rename(join(from, entry), join(to, entry));
  }
}

async function main() {
  await ensureInstalled();
  const child = spawn(EXE, process.argv.slice(2), {
    stdio: "inherit",
    windowsHide: true
  });

  child.on("exit", (code, signal) => {
    if (signal) {
      process.kill(process.pid, signal);
      return;
    }
    process.exit(code ?? 0);
  });
}

main().catch((error) => {
  console.error(`[wpfpilot-mcp] ${error.message}`);
  process.exit(1);
});
