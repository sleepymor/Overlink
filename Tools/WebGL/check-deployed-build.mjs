// Reports which Google-sign-in fixes are in the build itch.io is actually serving.
//
//   node Tools/WebGL/check-deployed-build.mjs "https://sleepymor.itch.io/<game>?secret=<unlisted-secret>"
//
// Takes either the game page URL or a frame directory, and no default: see the argument handling below.
//
// Why this exists: every WebGL Google sign-in round so far has been diagnosed from a browser console, and
// a console belongs to whichever build the tab loaded. A tab left open across a deploy keeps reporting the
// previous build, which turns "the fix did not work" out of a real signal into an artefact - one round here
// was spent on exactly that. The deployed bridge is a public static file, so the question "is my fix live?"
// is answerable in one request without playing the game.
//
// Lives under Tools/ rather than Assets/ so Unity cannot import it, for the same reason as the bridge harness
// next to it: a .js inside Assets/ would meet the legacy UnityScript importer.

const FRAME_BASE_PATTERN = /https?:\/\/[^"'&<>\s]*(?:itch\.zone|hwcdn\.net)\/html\/[^"'&<>\s]*?index\.html(?:\?[^"'&<>\s]*)?/;

// Markers are strings that only a given round's bridge can emit, taken from
// Assets/Plugins/WebGL/OverlinkGoogleAuth.jslib. Ordered oldest first so the output reads as a timeline.
const MARKERS = [
  ["origin stamped on every diagnostic", "[origin: ", "round 1: the iframe's own origin in the log line"],
  ["GIS console tap", "GIS said (error): ", "round 1: Google's console-only complaints reach Unity's log"],
  ["new-tab escape hatch", "Sign in in a new tab", "round 2: a way out of the embed after a failure"],
  ["FedCM availability probe", "FedCM availability in this frame", "round 2: says whether itch delegates the permission"],
  ["handback watcher", "closed without returning a token", "round 3: names a popup that authenticated and returned nothing"],
  ["postMessage tap", "postMessage from origin", "round 3: makes an opaque-origin handback visible"],
];

// The FedCM flags are the whole point of round 2, and they are deliberately opposite, so report their live
// values as facts rather than hiding them behind a marker that could match either way.
const FLAGS = ["use_fedcm_for_prompt", "use_fedcm_for_button"];

function readFlags(text) {
  const out = {};
  for (const flag of FLAGS) {
    // Minifiers spell booleans as !0 / !1; accept both, and a value we do not recognise rather than
    // defaulting it to something reassuring.
    const m = text.match(new RegExp(flag + "\\s*:\\s*(true|false|!0|!1)"));
    if (!m) out[flag] = "absent";
    else out[flag] = m[1] === "true" || m[1] === "!0" ? "true" : "false";
  }
  return out;
}

function markersPresent(text) {
  const present = [];
  const absent = [];
  for (const [name, needle, why] of MARKERS) {
    (text.includes(needle) ? present : absent).push([name, why]);
  }
  return { present, absent };
}

async function getText(url) {
  let res;
  try {
    res = await fetch(url, { headers: { "user-agent": "Mozilla/5.0 (compatible; overlink-deploy-check)" } });
  } catch (e) {
    // A mistyped or scheme-less URL rejects here rather than returning a status. Report it as the usage
    // error it is instead of letting a stack trace stand in for one.
    return { status: 0, text: "", error: String(e && e.message || e) };
  }
  if (!res.ok) return { status: res.status, text: "" };
  return { status: 200, text: await res.text() };
}

// Accepts either the itch page URL (unlisted pages need their ?secret= form) or a frame base URL directly,
// so a known directory can be checked without re-resolving it. No default: a hardcoded frame directory rots
// the moment butler pushes, and a stale answer is the exact failure this script exists to catch.
const arg = process.argv[2];
if (!arg) {
  console.error("usage: node Tools/WebGL/check-deployed-build.mjs <itch page URL | frame base URL>");
  console.error("  e.g. \"https://sleepymor.itch.io/overlink?secret=...\"");
  process.exit(2);
}

let base;
let frameUrl = arg;
const asFrame = arg.match(/https?:\/\/[^"'\s]*\/html\/[^/"'\s]+/);
if (asFrame) {
  // A frame directory was given directly, so skip the page lookup. Anything after the directory - the
  // index.html, the ?v= stamp, itch's own query args - is dropped here and re-read from frameUrl below.
  base = asFrame[0];
} else {
  const page = await getText(arg);
  if (page.status !== 200) {
    if (page.error) console.error(`cannot fetch that URL: ${page.error}`);
    else console.error(`page ${page.status}: an unlisted itch page needs its ?secret= URL`);
    process.exit(2);
  }
  const found = page.text.match(FRAME_BASE_PATTERN);
  if (!found) {
    console.error("no itch.io game frame in that page - is the HTML5 build published and the embed enabled?");
    process.exit(2);
  }
  frameUrl = found[0];
  base = frameUrl.replace(/\/index\.html.*$/, "");
}

// The ?v= token is itch's cache stamp for this frame. It changes on every push, so alongside the directory
// it is what identifies the build a tab is actually running.
const stamp = frameUrl.match(/\?v=(\d+)/)?.[1] || "unknown";
console.log(`frame:  ${base}`);
console.log(`build:  ?v=${stamp}`);

// The framework file is the bridge. Unity emits .js, .js.br or .js.gz depending on the build's compression
// setting, and itch serves whichever is in the zip, so try each rather than assuming.
let framework = null;
let lastError = null;
for (const name of ["Overlink.framework.js.br", "Overlink.framework.js.gz", "Overlink.framework.js"]) {
  const url = `${base}/Build/${name}`;
  const got = await getText(url);
  if (got.error) lastError = got.error;
  if (got.status === 200 && got.text.length > 1000) {
    // A compressed response that the fetch layer did not decode looks like binary; say so instead of
    // reporting every marker as absent, which is the exact false negative this script exists to prevent.
    const mojibake = (got.text.match(/\uFFFD/g) || []).length;
    if (mojibake > got.text.length / 100) {
      console.error(`\n${name}: fetched but undecoded (${mojibake} replacement chars) - markers are unreliable`);
      process.exit(3);
    }
    framework = { name, text: got.text };
    break;
  }
}

if (!framework) {
  console.error(`\ncould not fetch the framework js under ${base}/Build/ - is this really a WebGL build?`);
  if (lastError) console.error(`last fetch error: ${lastError}`);
  process.exit(2);
}

const { present, absent } = markersPresent(framework.text);
const flags = readFlags(framework.text);
console.log(`bridge: ${framework.name} (${framework.text.length} chars decoded)\n`);
for (const flag of FLAGS) console.log(`  ${flag} = ${flags[flag]}`);
console.log();
for (const [heading, rows, mark] of [["present", present, "yes"], ["absent", absent, "no "]]) {
  if (!rows.length) continue;
  console.log(`${heading}:`);
  for (const [name, why] of rows) console.log(`  ${mark}  ${name}  - ${why}`);
}
if (!present.length) console.log("nothing recognised - this bridge predates every round it is being compared to.");

// The one judgement worth automating: round 2's fix is only a fix if it shipped.
if (flags.use_fedcm_for_button === "true") {
  console.log("\nWARNING: the button still uses FedCM, so gate 3 is unmitigated on this host.");
  process.exitCode = 1;
} else if (flags.use_fedcm_for_button === "absent") {
  console.log("\nWARNING: neither FedCM flag is in this bridge - it predates round 2 entirely.");
  process.exitCode = 1;
}
