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

// A DOMCSSStyleDeclaration stand-in. Assigning cssText really does populate the individual properties in
// a browser, and the bridge hides the failure line and the new-tab exit that way, so the stub has to
// parse rather than store the string opaquely - otherwise "hidden" is unobservable and a case that only
// sets cssText looks like it left the element visible.
function makeStyle() {
  const style = {};
  Object.defineProperty(style, "cssText", {
    enumerable: false,
    get() {
      return Object.keys(style)
        .map((name) => `${name}: ${style[name]};`)
        .join(" ");
    },
    set(text) {
      for (const decl of String(text).split(";")) {
        const at = decl.indexOf(":");
        if (at < 0) continue;
        const name = decl.slice(0, at).trim();
        if (name) style[name] = decl.slice(at + 1).trim();
      }
    },
  });
  return style;
}

function makeElement(tag) {
  return {
    tagName: tag,
    style: makeStyle(),
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

// window.open is the escape hatch's whole surface, so it is recorded rather than merely tolerated: a
// bridge that opens a tab nobody asked for, or forgets to finish the flow afterwards, is a bug here.
global.window = {
  location: { origin: "https://html5.itch.zone", href: "https://html5.itch.zone/7000/abc/index.html" },
  open: (url, target) => {
    opened.push({ url, target });
    return { fake: "window" };
  },
};
const opened = [];

// navigator.permissions stays absent by default, because the bridge must cope with a browser that has
// no opinion about publickey-credentials-get. Cases that care about the FedCM probe install it. Only
// window.navigator is ever touched: Node's own global navigator is getter-only and ESM is strict.
function installPermissions(state) {
  window.navigator = {
    permissions: {
      query: () => Promise.resolve({ state }),
    },
  };
}

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
  opened.length = 0;
  gsiLog.length = 0;
  document.body.children.length = 0;
  document.head.children.length = 0;
  resetTimers();
  // A case that installed a permissions stub or a blocking window.open must not leak it into the next.
  window.navigator = undefined;
  window.open = (url, target) => {
    opened.push({ url, target });
    return { fake: "window" };
  };
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

// Drains the microtask queue, so a bridge that reports from inside a promise callback (the FedCM probe)
// can be asserted on synchronously. process.nextTick is a real API and deliberately not stubbed, unlike
// setTimeout, which the fake-timer machinery owns.
const flush = () => new Promise((resolve) => process.nextTick(resolve));

// What GIS itself would have printed. The bridge's tap is expected to lift this into Unity's log.
function gsiSays(text) {
  global.console.error(text);
}
function gsiSaysWarn(text) {
  global.console.warn(text);
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
const allDiags = () => sent.filter((s) => s[1] === "ReceiveDiagnostic");
const diag = () => {
  const all = allDiags();
  return all.length ? all[all.length - 1] : undefined;
};
const token = () => sent.find((s) => s[1] === "ReceiveToken");
const overlay = () => document.body.children[0];
const card = () => overlay().children[0];

// Located by id rather than by position, because the card's layout is presentation and the bridge is
// free to reorder it; the ids are the contract this harness holds the bridge to.
function byId(id, node) {
  node = node || document.body;
  for (const child of node.children || []) {
    if (child.id === id) return child;
    const found = byId(id, child);
    if (found) return found;
  }
  return null;
}

// Asserting on a control that a broken bridge never rendered must produce a readable failure, not a
// TypeError that aborts the run before the remaining cases report. Same for pressing one.
const styleProp = (id, prop) => {
  const el = byId(id);
  return el ? el.style[prop] : "<missing>";
};
const press = (id) => {
  const el = byId(id);
  if (!el || typeof el.onclick !== "function") return false;
  el.onclick();
  return true;
};

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
  press("overlink-gsi-cancel");
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
  check("10 overlay attached", !!ov && ov.parentNode !== null, "nothing to click: no overlay in the document");
  if (ov) {
    ov.onclick({ target: card() });
    check("10 card click ignored", !token(), "a card click cancelled the flow");
    ov.onclick({ target: ov });
    check("10 backdrop click cancels", token() && token()[2] === "", JSON.stringify(token()));
  }
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

// Case 15: the transport choice, which is the whole of "the popup closes on Processing your request".
// FedCM for the button exchanges the credential via navigator.credentials.get() from GIS's iframe, and
// a third-party frame is refused unless the embedder delegates publickey-credentials-get. The legacy
// transport does the exchange in its own top-level popup. Both flags are asserted because they are
// deliberately opposite, and a future edit that "tidies them up" into agreement reintroduces the bug.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("opt_out_or_no_session")) });
  invoke(loadBridge());
  check("15 button transport is not FedCM", gsi.initializedWith?.use_fedcm_for_button === false,
    "use_fedcm_for_button not disabled: " + JSON.stringify(gsi.initializedWith?.use_fedcm_for_button));
  check("15 One Tap still uses FedCM", gsi.initializedWith?.use_fedcm_for_prompt === true,
    "use_fedcm_for_prompt not set");
  check("15 subscribes to GIS errors", typeof gsi.initializedWith?.error_callback === "function",
    "no error_callback: a chooser that dies silently would go unreported");
}

// Case 16: GIS reports an error mid-flow. This is a dead attempt, not a dead session: the button on
// screen still works, so the card must survive and offer a way out. The first version of this bridge
// treated every non-display as final, which is what made the itch.io failure unfixable from the log.
{
  reset();
  installGsi({
    prompt: (cb) => cb(notDisplayed("iframe")),
    render: (host) => host.appendChild(makeElement("iframe")),
  });
  invoke(loadBridge());
  gsi.initializedWith.error_callback?.({
    type: "popup_closed",
    details: { message: "User closed the popup window" },
  });
  check("16 soft error sends no token", !token(), "a recoverable error ended the flow");
  check("16 card survives", !!overlay() && overlay().parentNode !== null, "overlay torn down on a retryable error");
  check("16 names the error type", diag() && /popup_closed/.test(diag()[2]), JSON.stringify(diag() && diag()[2]));
  check("16 carries GIS's own detail", diag() && /User closed the popup window/.test(diag()[2]),
    "detail flattened away: " + JSON.stringify(diag() && diag()[2]));
  check("16 failure line is shown", styleProp("overlink-gsi-failure", "display") === "block",
    "player is left with no explanation: " + styleProp("overlink-gsi-failure", "display"));
  check("16 new-tab exit is offered", styleProp("overlink-gsi-newtab", "display") === "inline-block",
    "no escape from an iframe Google will not sign in inside: " + styleProp("overlink-gsi-newtab", "display"));

  // The retry is what the surviving card was for.
  gsi.initializedWith.callback({ credential: "jwt.after-retry" });
  check("16 retry delivers the token", token() && token()[2] === "jwt.after-retry", JSON.stringify(token()));
  check("16 overlay gone after success", !overlay() || overlay().parentNode === null, "overlay left behind");
}

// Case 17: the failure line and the escape hatch are invisible until there is a failure. A working
// button must not advertise a way out of the iframe, and must not show a gap of reserved space.
{
  reset();
  installGsi({
    prompt: (cb) => cb(notDisplayed("iframe")),
    render: (host) => host.appendChild(makeElement("iframe")),
  });
  invoke(loadBridge());
  check("17 failure line hidden", styleProp("overlink-gsi-failure", "display") === "none",
    styleProp("overlink-gsi-failure", "display"));
  check("17 new-tab exit hidden", styleProp("overlink-gsi-newtab", "display") === "none",
    styleProp("overlink-gsi-newtab", "display"));
  check("17 cancel always available", styleProp("overlink-gsi-cancel", "display") !== "none",
    "the only guaranteed exit was hidden: " + styleProp("overlink-gsi-cancel", "display"));
}

// Case 18: the escape hatch. Reloading the game top-level is the one route that works when the embed
// itself is the obstruction, so it has to be reachable - and it has to end this flow, or the old tab
// keeps holding AccountManager.IsBusy for three minutes while the player signs in next door.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  press("overlink-gsi-newtab");
  check("18 opened exactly one tab", opened.length === 1, JSON.stringify(opened));
  check("18 the tab is this game", opened[0] && /^https:\/\//.test(opened[0].url),
    "opened a nonsense URL: " + JSON.stringify(opened[0]));
  check("18 opened as a new browsing context", opened[0] && opened[0].target === "_blank",
    JSON.stringify(opened[0] && opened[0].target));
  check("18 flow is closed out", token() && token()[2] === "", JSON.stringify(token()));
  check("18 overlay removed", !overlay() || overlay().parentNode === null, "overlay left behind");
}

// Case 19: the escape hatch blocked by a popup blocker. Named, not hung.
{
  reset();
  window.open = () => null;
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  press("overlink-gsi-newtab");
  check("19 no phantom tab", opened.length === 0, "a tab was recorded despite window.open returning null");
  check("19 reports unavailable", diag() && diag()[2].startsWith("unavailable|"), JSON.stringify(diag()));
  check("19 says what to do", diag() && /pop-?ups/i.test(diag()[2]),
    "detail does not help: " + JSON.stringify(diag() && diag()[2]));
}

// Case 20: the FedCM probe. When the browser has an opinion about publickey-credentials-get, the state
// is exactly the fact that explains a chooser closing on "Processing your request...", so it is logged.
{
  reset();
  installPermissions("denied");
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  await flush();
  check("20 logs the FedCM verdict", allDiags().some((d) => /publickey-credentials-get=denied/.test(d[2])),
    "verdict never reported: " + JSON.stringify(allDiags().map((d) => d[2])));
  check("20 verdict is not terminal", !token(), "an informational probe ended the flow");
}

// Case 21: a browser with no permissions API for that name (or no navigator at all) must say nothing
// rather than throw. The probe is diagnostics, not a dependency.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  await flush();
  check("21 silent without the permissions API",
    !allDiags().some((d) => /publickey-credentials-get/.test(d[2])),
    "reported a verdict it never got: " + JSON.stringify(allDiags().map((d) => d[2])));
  check("21 still rendered the button", gsi.renderedInto !== null, "probe failure blocked the fallback");
}

// Case 22: a diagnostic that resolves after the flow is over must not overwrite the outcome C# already
// delivered. This is the race the probe introduces: it is async, and the button is not.
{
  reset();
  installPermissions("denied");
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  gsi.initializedWith.callback({ credential: "jwt.first" });
  const before = allDiags().length;
  await flush();
  check("22 nothing reported after finish", allDiags().length === before,
    `late report slipped through: ${before} -> ${allDiags().length}`);
}

// Case 23: error_callback payloads have shipped in several shapes. None of them may throw, and all of
// them must still name the type.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("iframe")) });
  invoke(loadBridge());
  gsi.initializedWith.error_callback?.({ type: "idp_timeout" });
  check("23 bare object names its type", diag() && /idp_timeout/.test(diag()[2]), JSON.stringify(diag()));
  check("23 bare object admits no detail", diag() && /no detail/.test(diag()[2]),
    "invented a detail: " + JSON.stringify(diag() && diag()[2]));
  gsi.initializedWith.error_callback?.(null);
  check("23 null payload survives", diag() && /unknown/.test(diag()[2]), JSON.stringify(diag()));
  check("23 null payload still non-terminal", !token(), "a malformed error ended the flow");
}

// Case 24: the two GSI_LOGGER levels are not interchangeable, and this one is a trap the bridge sets for
// itself. use_fedcm_for_button: false logs a deprecation notice on *every* flow, so a gsiComplaint() that
// quoted whichever line arrived last would staple that warning onto an unrelated origin failure and send
// whoever reads the log next after a red herring. The error must win even when it arrived first.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("invalid_origin")) });
  invoke(loadBridge());
  gsiSays("[GSI_LOGGER]: The given origin is not allowed for the given client ID.");
  gsiSaysWarn("[GSI_LOGGER]: use_fedcm_for_button is deprecated and will stop working soon.");
  advance(6000);
  check("24 quotes the error, not the later warning",
    diag() && /GIS said \(error\): [^\n]*given origin is not allowed/.test(diag()[2]),
    "wrong line quoted: " + JSON.stringify(diag() && diag()[2]));
  check("24 deprecation notice never masquerades as the cause", diag() && !/deprecated/.test(diag()[2]),
    "the standing warning leaked into a failure it had nothing to do with: " + JSON.stringify(diag() && diag()[2]));
}

// Case 25: a warning on its own is still worth reporting, as long as it is labelled as one.
{
  reset();
  installGsi({ prompt: (cb) => cb(notDisplayed("invalid_origin")) });
  invoke(loadBridge());
  gsiSaysWarn("[GSI_LOGGER]: something GIS felt obliged to mention");
  advance(6000);
  check("25 lone warning is labelled",
    diag() && /GIS said \(warning\): \[GSI_LOGGER\]: something GIS felt obliged to mention/.test(diag()[2]),
    "warning lost or unlabelled: " + JSON.stringify(diag() && diag()[2]));
}

const failed = results.filter((r) => !r.pass);
for (const r of failed) say(`FAIL ${r.name} :: ${r.detail}`);
say(`${results.length - failed.length}/${results.length} assertions passed  (${path.relative(process.cwd(), jslibPath)})`);
process.exit(failed.length ? 1 : 0);
