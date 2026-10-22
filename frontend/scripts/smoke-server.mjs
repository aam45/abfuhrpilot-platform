// Minimal static file server for the CI-safe static build smoke check.
//
// Serves a built Angular `dist/**/browser` directory and rewrites any request that
// does not match a real file to `index.html`, mirroring how nginx/docker-compose
// already serves this app in production. This is required because the app uses
// Angular's path-based router (no `withHashLocation`), so client-side routes like
// `/login` have no matching file on disk.
//
// Deliberately dependency-free: uses only Node's built-in `http`/`fs`/`path` so this
// smoke check adds zero new npm packages.

import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, normalize, resolve, sep } from 'node:path';

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.ico': 'image/x-icon',
  '.webmanifest': 'application/manifest+json',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.txt': 'text/plain; charset=utf-8',
};

/**
 * Starts a static server rooted at `rootDir` with SPA fallback to `index.html`.
 * Returns `{ server, port, close }`.
 */
export function startStaticServer(rootDir, { port = 0 } = {}) {
  const server = createServer(async (req, res) => {
    try {
      const requestPath = decodeURIComponent((req.url ?? '/').split('?')[0]);
      const safePath = normalize(requestPath).replace(/^(\.\.[/\\])+/, '');
      let filePath = join(rootDir, safePath);

      // Belt-and-braces containment check (this only ever serves 127.0.0.1 inside a
      // throwaway CI job over its own already-public build output, so this isn't
      // guarding a real exposure — it's a correctness guarantee, not a guess).
      const resolvedRoot = resolve(rootDir);
      if (resolve(filePath) !== resolvedRoot && !resolve(filePath).startsWith(resolvedRoot + sep)) {
        filePath = join(rootDir, 'index.html');
      }

      let fileStat = await stat(filePath).catch(() => null);
      if (fileStat?.isDirectory()) {
        filePath = join(filePath, 'index.html');
        fileStat = await stat(filePath).catch(() => null);
      }
      if (!fileStat) {
        // SPA fallback: any unmatched path (e.g. `/login`) serves the app shell,
        // exactly like the production nginx config's try_files fallback.
        filePath = join(rootDir, 'index.html');
      }

      const body = await readFile(filePath);
      const type = MIME_TYPES[extname(filePath)] ?? 'application/octet-stream';
      res.writeHead(200, { 'Content-Type': type });
      res.end(body);
    } catch (err) {
      res.writeHead(500, { 'Content-Type': 'text/plain' });
      res.end(String(err));
    }
  });

  return new Promise((resolve, reject) => {
    server.on('error', reject);
    server.listen(port, '127.0.0.1', () => {
      const address = server.address();
      const boundPort = typeof address === 'object' && address ? address.port : port;
      resolve({
        server,
        port: boundPort,
        close: () => new Promise((res) => server.close(() => res(undefined))),
      });
    });
  });
}
