// Development harness for Assets/Plugins/WebGL/OverlinkGoogleAuth.jslib.
//
//     node Tools/WebGL/gsi-bridge-test.mjs
//
// Why this exists: the bridge is the only part of the auth layer that Unity's test runner cannot see.
// A .jslib is not compiled into any assembly, so nothing in Assets/Tests can reference it, and its
// failure modes only appear in a WebGL build - which costs a full CI cycle to discover. This file stubs
// the pieces of the browser that Unity's WebGL runtime supplies (mergeInto, UTF8ToString, SendMessage,
// the DOM, and google.accounts.id) so the bridge's control flow can be exercised in a second.
//
// It lives outside Assets/ on purpose: Unity would otherwise try to import it, and a .js file under
// Assets/ collides with the legacy UnityScript importer.
//
// The interesting cases are the ones that used to fail silently. The original bridge treated every
// non-display from One Tap as a player cancellation and sent an empty token, which C# rendered as
// "Google sign-in was cancelled." on itch.io, where nobody had cancelled anything. Case 1 is that bug.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const jslibPath = process.argv[2]
  ?? path.resolve(here, "..", "..", "Assets", "Plugins", "WebGL", "OverlinkGoogleAuth.jslib");

// Captured before anything replaces it. The bridge taps console.error/console.warn, and the harness
// stubs the whole console to observe that, so the report needs its own handle on real stdout.
const realConsole = console;

const sent = []; // [gameObject, method, arg]

global.LibraryManager = { library: {} };
global.mergeInto = (target, src) => Object.assign(target, src);
global.UTF8ToString = (s) => s;
global.SendMessage = (go, method, arg) => sent.push([go, method, arg]);

function makeElement(tag) {
  return {
    tagName: tag,
    style: { cssText: "" },
    children: [],
    parentNode: null,
    textContent: "",
    id: "",
    get firstChild() {
      return this.children.length ? this.children[0] : null;
    },
    appendChild(child) {
      child.parentNode = this;
      this.children.push(child);
      return child;
    },
    removeChild(child) {
      const at = this.children.indexOf(child);
      if (at >= 0) this.children.splice(at, 1);
      child.parentNode = null;
      return child;
    },
  };
}

global.document = {
  head: makeElement("head"),
  body: makeElement("body"),
  createElement: makeElement,
};
global.window = { location: { origin: "https://html5.itch.zone" } };

// Fake timers: the bridge polls for a rendered button and arms a console-tap deadline, both of which
// must be driven deterministically rather than awaited.
let timers = [];
let timerSeq = 0;
let clock = 0;

global.setInterval = (fn, ms) => {
  const id = ++timerSeq;
  timers.push({ id, fn, period: ms, next: clock + ms, repeat: true });
  return id;
};
global.setTimeout = (fn, ms) => {
  const id = ++timerSeq;
  timers.push({ id, fn, period: ms, next: clock + ms, repeat: false });
  return id;
};
const removeTimer = (id) => {
  timers = timers.filter((t) => t.id !== id);
};
global.clearInterval = removeTimer;
global.clearTimeout = removeTimer;

// Runs every timer due within the next `ms` of virtual time, oldest first, rescheduling intervals.
// The guard exists so a poll loop that never clears itself cannot hang the harness.
function advance(ms) {
  const target = clock + ms;
  for (let guard = 0; guard < 10000; guard++) {
    const due = timers.filter((t) => t.next <= target).sort((a, b) => a.next - b.next)[0];
    if (!due) break;
    clock = due.next;
    if (due.repeat) due.next = clock + due.period;
    else removeTimer(due.id);
    due.fn();
  }
  clock = target;
}

function resetTimers() {
  timers = [];
  timerSeq = 0;
  clock = 0;
}

// --- Google Identity Services stub -------------------------------------------------------------

let gsi = null;

function installGsi(behavior) {
  gsi = {
    initializedWith: null,
    prompted: false,
    canceled: 0,
    renderedInto: null,
    initialize(cfg) {
      gsi.initializedWith = cfg;
    },
    prompt(cb) {
      gsi.prompted = true;
      behavior.prompt(cb);
    },
    cancel() {
      gsi.canceled++;
    },
    renderButton(host, opts) {
      gsi.renderedInto = host;
      gsi.renderOpts = opts;
      if (behavior.render) behavior.render(host, opts);
    },
  };
  window.google = { accounts: { id: gsi } };
}

const notDisplayed = (reason) => ({
  isNotDisplayed: () => true,
  getNotDisplayedReason: () => reason,
  isSkippedMoment: () => false,
  isDismissedMoment: () => false,
});
const skipped = (reason) => ({
  isNotDisplayed: () => false,
  isSkippedMoment: () => true,
  getSkippedReason: () => reason,
  isDismissedMoment: () => false,
});
const dismissed = (reason) => ({
  isNotDisplayed: () => false,
  isSkippedMoment: () => false,
  isDismissedMoment: () => true,
  getDismissedReason: () => reason,
});

function reset() {
  sent.length = 0;
  document.body.children.length = 0;
  document.head.children.length = 0;
  resetTimers();
  gsiLog.length = 0;
  // A fresh console each case, so a bridge that fails to restore its tap shows up as a failure rather
  // than silently poisoning the next case. say() keeps a handle on the real stdout, because this stub
  // replaces the very console the report is printed through.
  global.console = {
    error: (...a) => gsiLog.push(["error", a.join(" ")]),
    warn: (...a) => gsiLog.push(["warn", a.join(" ")]),
    log: () => {},
  };
}

const say = (...args) => realConsole.log(...args);
const gsiLog = [];

// What GIS itself would have printed. The bridge's tap is expected to lift this into Unity's log.
function gsiSays(text) {
  global.console.error(text);
}

// Re-evaluated per case so each one starts from a fresh closure, exactly as a fresh page load would.
function loadBridge() {
  const source = fs.readFileSync(jslibPath, "utf8");
  new Function("mergeInto", "LibraryManager", "UTF8ToString", "SendMessage", source)(
    global.mergeInto,
    global.LibraryManager,
    global.UTF8ToString,
    global.SendMessage
  );
  return global.LibraryManager.library.OverlinkGoogleSignIn;
}

function invoke(fn) {
  fn("client-id", "nonce-123", "OverlinkGoogleAuthReceiver", "ReceiveToken");
}

// The bridge may report several times in one flow (a fallback notice, then the eventual failure), so
// the assertion target is always the LAST diagnostic: that is the one C# hands to the player.
const diag = () => {
  const all = sent.filter((s) => s[1] === "ReceiveDiagnostic");
  return all.length ? all[all.length - 1] : undefined;
};
const token = () => sent.find((s) => s[1] === "ReceiveToken");
const overlay = () => document.body.children[0];
const card = () => overlay().children[0];

const results = [];
function check(name, cond, detail) {
  results.push({ name, pass: !!cond, detail: cond ? "" : detail || "" });
}

// Case 1: the itch.io reality - One Tap cannot display inside a third-party iframe.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());

  check("1 falls back to the Google button", gsi.renderedInto !== null, "renderButton never ran");
  check("1 attaches the overlay", !!overlay(), "no overlay in document.body");
  check("1 sends no token yet", !token(), "sent a token prematurely: " + JSON.stringify(token()));
  check("1 tears down One Tap first", gsi.canceled === 1, `id.cancel ran ${gsi.canceled}x`);
  check("1 opts into FedCM", gsi.initializedWith?.use_fedcm_for_prompt === true,
    "use_fedcm_for_prompt not set");
  check("1 passes the nonce", gsi.initializedWith?.nonce === "nonce-123", "nonce missing");
  check("1 passes the client id", gsi.initializedWith?.client_id === "client-id", "client id missing");
  check("1 logs GIS's reason", diag() && /iframe/.test(diag()[2]),
    "reason not reported: " + JSON.stringify(diag() && diag()[2]));
  check("1 names its own origin", diag() && /\[origin: https:\/\/html5\.itch\.zone\]/.test(diag()[2]),
    "origin missing: " + JSON.stringify(diag() && diag()[2]));

  // The player then dismisses the fallback.
  card().children[2].onclick();
  check("1 dismiss reports cancelled", diag() && diag()[2].startsWith("cancelled|"), JSON.stringify(diag()));
  check("1 dismiss sends an empty token", token() && token()[2] === "", JSON.stringify(token()));
  check("1 overlay is torn down", !overlay() || overlay().parentNode === null, "overlay left behind");

  // A credential arriving after the dismissal must not resurrect the flow.
  gsi.initializedWith.callback({ credential: "late-token" });
  check("1 double-finish is guarded", sent.filter((s) => s[1] === "ReceiveToken").length === 1,
    "a second ReceiveToken fired after finish");
}

// Case 2: the fallback succeeds - the credential reaches C# and the overlay disappears.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("unsupported_browser")) });
  invoke(loadBridge());
  gsi.initializedWith.callback({ credential: "jwt.here.sig" });
  check("2 credential forwarded", token() && token()[2] === "jwt.here.sig", JSON.stringify(token()));
  check("2 overlay removed", !overlay() || overlay().parentNode === null, "overlay left behind");
}

// Case 3: GIS invokes the callback with nothing in it. A provider failure, not a cancellation.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("invalid_origin")) });
  invoke(loadBridge());
  gsi.initializedWith.callback({});
  check("3 reports failed", diag() && diag()[2].startsWith("failed|"), JSON.stringify(diag()));
  check("3 empty token", token() && token()[2] === "", JSON.stringify(token()));
}

// Case 4: One Tap showed and the player explicitly cancelled. Must stay a cancellation,
// not a fallback - rendering a button at that point would be refusing to take no for an answer.
{
  reset();
  installGsi({ prompt: (cb) => cb(skipped("user_cancel")) });
  invoke(loadBridge());
  check("4 no button rendered", gsi.renderedInto === null, "fell back on an explicit cancel");
  check("4 cancelled code", diag() && diag()[2].startsWith("cancelled|"), JSON.stringify(diag()));
}

// Case 5: One Tap skipped for a non-cancel reason (no Google session) - try the button.
{
  reset();
  installGsi({ prompt: (cb) => cb(skipped("no_session")) });
  invoke(loadBridge());
  check("5 renders the button", gsi.renderedInto !== null, "no fallback on no_session");
}

// Case 6: dismissed with credential_returned. The callback owns the handoff, so say nothing.
{
  reset();
  installGsi({ prompt: (cb) => cb(dismissed("credential_returned")) });
  invoke(loadBridge());
  check("6 stays silent", sent.length === 0, "spurious messages: " + JSON.stringify(sent));
}

// Case 7: renderButton throws - GIS is present but unusable in this embed.
{
  reset();
  installGsi({
    prompt: (cb) => cb(notDisplayed("iframe")),
    render: () => {
      throw new Error("cannot render here");
    },
  });
  invoke(loadBridge());
  check("7 reports unavailable", diag() && diag()[2].startsWith("unavailable|"), JSON.stringify(diag()));
  check("7 names the cause", diag() && /cannot render here/.test(diag()[2]), "detail lost");
  check("7 empty token", token() && token()[2] === "", JSON.stringify(token()));
}

// Case 8: the host page's CSP blocks accounts.google.com, so GIS never loads at all.
{
  reset();
  window.google = undefined;
  const fn = loadBridge();
  invoke(fn);
  const script = document.head.children[0];
  check("8 requests the GIS script", script && /accounts\.google\.com\/gsi\/client/.test(script.src),
    "no script injected");
  script.onerror();
  check("8 reports unavailable", diag() && diag()[2].startsWith("unavailable|"), JSON.stringify(diag()));
  check("8 blames the script load", diag() && /gsi\/client/.test(diag()[2]), "detail lost");
}

// Case 9: GIS is already present from a previous attempt, so no second script tag and straight to prompt.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  check("9 no duplicate script", document.head.children.length === 0, "injected a script despite GIS being present");
  check("9 prompted", gsi.prompted === true, "prompt never ran");
}

// Case 10: a backdrop click dismisses, but a click inside the card must not.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  const ov = overlay();
  ov.onclick({ target: card() });
  check("10 card click ignored", !token(), "a card click cancelled the flow");
  ov.onclick({ target: ov });
  check("10 backdrop click cancels", token() && token()[2] === "", JSON.stringify(token()));
}

// Case 11: the real itch.io failure. The OAuth client rejects the origin, so renderButton neither
// throws nor draws anything. The bridge must notice and fail fast, not park the player on an empty card
// until the three-minute deadline.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("invalid_origin")) });
  invoke(loadBridge());
  gsiSays("[GSI_LOGGER]: The given origin is not allowed for the given client ID.");
  check("11 nothing rendered", gsi.renderedInto !== null && !gsi.renderedInto.firstChild,
    "stub did not model the rejection");
  advance(4500);
  check("11 still waiting at 4.5s", !token(), "gave up before the grace period elapsed");
  advance(1000);
  check("11 reports unavailable", diag() && diag()[2].startsWith("unavailable|"), JSON.stringify(diag()));
  check("11 empty token", token() && token()[2] === "", JSON.stringify(token()));
  check("11 names the origin problem", diag() && /Authorized JavaScript origins/.test(diag()[2]),
    "detail does not explain: " + JSON.stringify(diag() && diag()[2]));
  check("11 quotes GIS's own complaint", diag() && /given origin is not allowed/.test(diag()[2]),
    "GSI_LOGGER line never reached the diagnostic: " + JSON.stringify(diag() && diag()[2]));
  check("11 overlay is torn down", !overlay() || overlay().parentNode === null, "overlay left behind");
}

// Case 12: a correctly configured client. The button appears a beat later, and the bridge must not
// mistake that for a rejection.
{
  reset();
  installGsi({
    prompt: (cb) => cb(notDisplayed("iframe")),
    render: (host) => host.appendChild(makeElement("iframe")),
  });
  invoke(loadBridge());
  advance(20000);
  check("12 no false failure", !token(), "bridge rejected a button that did render: " + JSON.stringify(diag()));
  check("12 overlay survives", !!overlay(), "overlay torn down while the button was still usable");
}

// Case 13: the console tap is a borrowed tool. It has to go back, or the build ships a global hook that
// outlives the auth flow it was installed for.
{
  reset();
  const before = { error: global.console.error, warn: global.console.warn };
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  check("13 tap installed during the flow", global.console.error !== before.error, "console was never tapped");
  gsiSays("[GSI_LOGGER]: noise that should still reach the real console");
  check("13 originals still called", gsiLog.some((l) => /still reach the real console/.test(l[1])),
    "the tap swallowed console output instead of delegating");
  gsi.initializedWith.callback({ credential: "jwt.a.b" });
  check("13 tap removed on success", global.console.error === before.error && global.console.warn === before.warn,
    "console hook leaked past the end of the flow");
}

// Case 14: GIS never answers at all. The deadline must still take the tap off the console rather than
// leaving it wired in for the life of the page.
{
  reset();
  const before = { error: global.console.error, warn: global.console.warn };
  installGsi({ prompt: () => {} });
  invoke(loadBridge());
  advance(4 * 60 * 1000);
  check("14 tap removed on deadline", global.console.error === before.error,
    "console hook outlived a flow that never finished");
}

const failed = results.filter((r) => !r.pass);
for (const r of failed) say(`FAIL ${r.name} :: ${r.detail}`);
say(`${results.length - failed.length}/${results.length} assertions passed  (${path.relative(process.cwd(), jslibPath)})`);
process.exit(failed.length ? 1 : 0);
