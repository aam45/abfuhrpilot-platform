// CI-safe static build smoke check.
//
// Serves the already-built `dist/frontend/browser` output with `smoke-server.mjs` and
// uses the CI-provided headless Chrome binary (`CHROME_BIN`, same one Karma already
// uses) in `--dump-dom` mode to assert:
//   1. The app shell (`/`) renders — the router redirects unauthenticated visitors to
//      `/login`, so the dumped DOM must contain the login screen, not a blank shell.
//   2. `/login` renders the actual login form (German copy + form fields), not an
//      empty `<app-root>`.
//
// This intentionally does NOT attempt a real authenticated flow (no Postgres/Docker
// in this CI job); a full authenticated end-to-end suite is out of scope here.
// It adds zero new npm dependencies: it shells out to the Chrome binary CI already installs
// via `browser-actions/setup-chrome@v1` for Karma.
//
// Usage: node scripts/smoke-check.mjs
// Env:   CHROME_BIN - path to a Chrome/Chromium/Edge executable (required)

import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { startStaticServer } from './smoke-server.mjs';

const execFileAsync = promisify(execFile);

const FRONTEND_ROOT = fileURLToPath(new URL('..', import.meta.url));
const DIST_CANDIDATES = [
  join(FRONTEND_ROOT, 'dist', 'frontend', 'browser'),
  join(FRONTEND_ROOT, 'dist', 'frontend'),
];

const CHECKS = [
  {
    path: '/',
    label: 'app shell (/ -> redirects unauthenticated users to /login)',
    // Unauthenticated "/" is redirected client-side to the login screen by authGuard.
    mustContain: ['app-root', 'Anmelden'],
  },
  {
    path: '/login',
    label: 'login route (/login)',
    mustContain: ['app-root', 'Anmelden', 'AbfuhrPilot', 'id="email"', 'id="pw"'],
  },
];

function findDistRoot() {
  for (const candidate of DIST_CANDIDATES) {
    if (existsSync(join(candidate, 'index.html'))) return candidate;
  }
  throw new Error(
    `No built Angular app found. Looked for index.html under: ${DIST_CANDIDATES.join(', ')}. ` +
      'Run `npx ng build` before this smoke check.',
  );
}

async function dumpDom(chromeBin, url) {
  // `--virtual-time-budget` behavior is not fully consistent across Chrome versions in
  // new headless mode (some builds fall back to wall-clock navigation instead of honoring
  // it). The outer `timeout: 30_000` below is the real safety net for that — do not remove
  // it while trying to "fix" the 5000ms budget value.
  const { stdout } = await execFileAsync(
    chromeBin,
    [
      '--headless=new',
      '--disable-gpu',
      '--no-sandbox',
      '--disable-dev-shm-usage',
      '--virtual-time-budget=5000',
      '--dump-dom',
      url,
    ],
    { maxBuffer: 10 * 1024 * 1024, timeout: 30_000 },
  );
  return stdout;
}

async function main() {
  const chromeBin = process.env.CHROME_BIN;
  if (!chromeBin) {
    throw new Error('CHROME_BIN is not set. This check reuses the Chrome binary CI installs for Karma.');
  }

  const distRoot = findDistRoot();
  console.log(`[smoke-check] serving ${distRoot}`);

  const { port, close } = await startStaticServer(distRoot);
  console.log(`[smoke-check] listening on http://127.0.0.1:${port}`);

  const failures = [];
  try {
    for (const check of CHECKS) {
      const url = `http://127.0.0.1:${port}${check.path}`;
      console.log(`[smoke-check] checking ${check.label} at ${url}`);
      const dom = await dumpDom(chromeBin, url);
      const missing = check.mustContain.filter((marker) => !dom.includes(marker));
      if (missing.length > 0) {
        failures.push(`${check.label}: missing expected markers ${JSON.stringify(missing)}`);
      } else {
        console.log(`[smoke-check] OK: ${check.label}`);
      }
    }
  } finally {
    await close();
  }

  if (failures.length > 0) {
    console.error('[smoke-check] FAILED:');
    for (const failure of failures) console.error(`  - ${failure}`);
    process.exitCode = 1;
    return;
  }

  console.log('[smoke-check] all checks passed.');
}

main().catch((err) => {
  console.error('[smoke-check] error:', err?.stack ?? err);
  process.exitCode = 1;
});
