// Reproduces an itch.io-style embed without building the game: two origins on localhost, the parent
// page on :8001 framing a GIS test page on :8000. That is the exact context itch.io creates - a
// cross-origin iframe whose document is not the page the player is looking at.
//
//     node Tools/WebGL/iframe-probe/serve.mjs
//     open http://localhost:8001
//
// The child page mirrors Assets/Plugins/WebGL/OverlinkGoogleAuth.jslib: same initialize options, same
// One Tap -> button cascade, same notification introspection. It prints GIS's raw verdicts instead of
// translating them, because the verdicts are the thing worth seeing.
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));

const pages = {
  "/": "index.html", // the embedder, served from the "itch.io" origin
  "/index.html": "index.html",
  "/game.html": "game.html", // the game, served from the "itch.zone" origin
};

// Two ports, two origins. A host header would not do it: origin is scheme + host + port.
function serverFor(port, label) {
  return http
    .createServer((req, res) => {
      const routed = pages[new URL(req.url, "http://x").pathname];
      if (!routed) {
        res.writeHead(404).end("not found");
        return;
      }
      res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
      res.end(fs.readFileSync(path.join(here, routed)));
    })
    .listen(port, () => console.log(`${label.padEnd(9)} http://localhost:${port}`));
}

console.log("Overlink GIS iframe probe");
console.log("  parent (embedder) and child (game) are deliberately different origins.\n");
serverFor(8001, "embedder");
serverFor(8000, "game");
console.log("\nOpen http://localhost:8001 and click Continue with Google inside the frame.");
