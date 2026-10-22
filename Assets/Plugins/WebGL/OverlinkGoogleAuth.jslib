// Google Identity Services bridge for the WebGL player.
//
// This file has been rewritten twice, and each rewrite bought a fact that the previous version could
// not have known. The order matters, because it explains why the flow tries things in this sequence:
//
//   1. One Tap via google.accounts.id.prompt() - the first version's only strategy. It assumes GIS may
//      paint its own floating UI over the top-level page, which a game embedded in a third-party iframe
//      (every itch.io web play) cannot allow. The old code threw GIS's *reason* away and sent an empty
//      token, so the player was told "you cancelled" when nobody had, and the developer saw nothing.
//
//   2. A real Sign in with Google button rendered into a DOM overlay - the second version's fallback,
//      and on itch.io the path that actually runs, because the button is user-initiated and opens as an
//      ordinary popup rather than depending on GIS being allowed to draw over the page.
//
//   3. Getting the button's own credential exchange to survive the iframe - what the third version is
//      for. With the origin finally registered, the button drew and the account chooser opened, and then
//      closed itself on "Processing your request..." with no token. That is FedCM: see use_fedcm_for_button
//      below. It is a second, independent reason the same flow dies inside a third-party frame, and it
//      needed the previous two fixes to be visible at all.
//
// Two lessons generalise out of this, both baked into the code below:
//   - Every failure carries a machine-readable code plus GIS's raw words, split by C# into player-facing
//     text and a console log. A silent empty token is how this took three rounds.
//   - Nothing terminal is assumed until GIS itself gives up. A dead *attempt* must leave the button on
//     screen, because tearing down the overlay the player is looking at turns a retry into a relaunch.
//
// Nothing here is covered by Unity's test runner - a .jslib belongs to no assembly, so there is no
// Assets/Tests file that can reference it. Control flow is covered by Tools/WebGL/gsi-bridge-test.mjs
// (`node Tools/WebGL/gsi-bridge-test.mjs`), which stubs the browser and GIS and runs in a second.
// Keep that in step with any change below; the alternative is discovering a regression in a WebGL build.
//
// STATUS: dormant. GoogleTokenProvider.Supported is false in a WebGL build, so OverlinkGoogleSignIn is
// never called and this file compiles into a shipped page that does not use it. It is kept rather than
// deleted because the three iframe gates it was written against (origin, One Tap, FedCM credential
// exchange) are properties of itch.io and will still be there whenever Google sign-in is offered on web
// again - see docs/AUTH_FLOW.md. Do not "tidy up" this bridge on the assumption it is dead code, and do
// not extend it without re-reading that section.

// The handle C# uses to call off a flow it has stopped waiting for. The bridge owns the overlay and C#
// owns the deadline, so the two need exactly one shared point of contact, and it has to be reachable from
// a second export - which puts it outside the sign-in function's closure. Null while no flow is running.
var overlinkGoogleFlow = null;

mergeInto(LibraryManager.library, {
  OverlinkGoogleSignIn: function (clientIdPtr, noncePtr, receiverPtr, callbackPtr) {
    var clientId = UTF8ToString(clientIdPtr);
    var nonce = UTF8ToString(noncePtr);
    var receiver = UTF8ToString(receiverPtr);
    var callback = UTF8ToString(callbackPtr);

    var done = false;
    var overlay = null;
    var failureLine = null;
    var newTabButton = null;
    var strandTimer = null;

    // GIS reports the single most useful diagnostic in this whole flow, "The given origin is not
    // allowed for the given client ID", only to the browser console, which a shipped WebGL build has
    // no way to surface. Nobody reading Unity's log ever sees it, so a misconfigured OAuth client looks
    // identical to a player who changed their mind. Tapping console for the duration of the flow puts
    // that sentence in the log where it can be acted on. Narrow on purpose: GSI_LOGGER lines only, the
    // originals are always called through, and the hook is removed the moment the flow ends.
    var consoleTap = null;

    function installConsoleTap() {
      if (consoleTap || typeof console === 'undefined') return;
      var originals = { error: console.error, warn: console.warn };
      var last = { error: '', warn: '' };
      consoleTap = { originals: originals, last: last };
      function tap(level) {
        return function () {
          try {
            var text = '';
            for (var i = 0; i < arguments.length; i++) {
              if (typeof arguments[i] === 'string') text += (text ? ' ' : '') + arguments[i];
            }
            if (text.indexOf('GSI_LOGGER') >= 0) last[level] = text;
          } catch (e) {}
          return originals[level].apply(console, arguments);
        };
      }
      console.error = tap('error');
      console.warn = tap('warn');
    }

    function removeConsoleTap() {
      if (!consoleTap) return;
      console.error = consoleTap.originals.error;
      console.warn = consoleTap.originals.warn;
      consoleTap = null;
    }

    // Two passive spies, installed for the flow's duration and removed by finish() exactly like the
    // console tap. They exist because the failure has now moved past everything this bridge watches. The
    // account chooser opens, authenticates the player, and the *handback* - GIS's popup navigating to
    // accounts.google.com/gsi/transform and posting the ID token back to its opener - is where an
    // embedded frame dies. Nothing on that path reaches error_callback, so without these the symptom is
    // still "the window closed and no reason appeared".
    //
    //   - window.open is wrapped to record where GIS sent the player, single-use secrets redacted. If that
    //     window closes and no token ever arrives, it is now a named failure instead of a silence.
    //   - a message listener records the handback itself, including the case that matters most: an event
    //     whose origin is "null", which is what a sandboxed popup's postMessage looks like from the
    //     receiving side, and a plausible reason a fully authenticated sign-in still produces nothing.
    //
    // Both report as 'fallback' and never end the flow. A sandboxed popup is a hypothesis until the log
    // says so, and the button on screen may still work on the next click.
    var spies = null;

    function sanitizeUrl(url) {
      try {
        return String(url).replace(
          /([?&](?:nonce|state|hint|session_state|id_token|access_token|code|login_hint)=)[^&]*/gi,
          '$1<redacted>');
      } catch (e) { return '<unreadable url>'; }
    }

    // Shape only, never contents. GIS's handback payload can be large, and a bearer credential is not
    // something to staple into a diagnostic line that ends up in a support screenshot.
    function shapeOf(data) {
      try {
        if (typeof data === 'string') return 'string(' + data.length + ')';
        if (data && typeof data === 'object') {
          var keys = [];
          for (var k in data) {
            if (!Object.prototype.hasOwnProperty.call(data, k)) continue;
            keys.push(k);
            if (keys.length >= 8) break;
          }
          return '{' + keys.join(',') + '}';
        }
        return typeof data;
      } catch (e) { return '<unshapeable>'; }
    }

    function installSpies() {
      if (spies || typeof window === 'undefined') return;
      var state = { open: window.open, messages: 0, popups: [], muted: false };
      spies = state;

      if (typeof state.open === 'function') {
        var realOpen = state.open;
        window.open = function () {
          var win = null;
          try { win = realOpen.apply(window, arguments); } catch (e) { win = null; }
          var url = arguments.length > 0 && arguments[0] !== undefined ? String(arguments[0]) : '';
          state.popups.push({ url: url, win: win });
          // The escape hatch opens a window deliberately, and reporting our own navigation would be
          // noise dressed as a diagnosis.
          if (!state.muted) {
            report('fallback', 'sign-in window opened to ' + sanitizeUrl(url).slice(0, 200) +
              (win ? '' : '; the browser refused it (window.open returned null)'));
          }
          return win;
        };
      }

      state.onMessage = function (event) {
        if (state.messages >= 6) return;
        var origin = event && event.origin;
        var text = String(origin === null || origin === undefined ? '' : origin);
        // Google's handback, or the opaque origin a sandboxed popup reports. Anything else - itch's host
        // chatter, Unity's own messaging - would drown the one line this was installed for.
        if (!(text === 'null' || text === '' || /(^|\.)google\.com$/i.test(text))) return;
        state.messages++;
        report('fallback', 'postMessage from origin ' + (text || '<none>') + ': ' + shapeOf(event && event.data));
      };
      try { window.addEventListener('message', state.onMessage, false); } catch (e) {}

      state.watch = setInterval(function () {
        if (done) { clearInterval(state.watch); return; }
        for (var i = 0; i < state.popups.length; i++) {
          var popup = state.popups[i];
          // A null window was refused at open time and already reported there; watching it would fire
          // immediately on every blocked-popup case and bury the real one.
          if (!popup.win) continue;
          var closed = true;
          try { closed = !!popup.win.closed; } catch (e) { closed = true; }
          if (!closed) continue;
          // One named failure per flow: clearInterval below stops the watcher, so a second closed popup
          // is not reported and the player's screen does not fill with repeats.
          clearInterval(state.watch);
          report('failed', "Google's sign-in window closed without returning a token; it had reached " +
            sanitizeUrl(popup.url).slice(0, 160));
          revealFailure('Google closed its sign-in window before handing the token back. Try again, ' +
            'or continue in a new tab.');
          return;
        }
      }, 1000);
    }

    function removeSpies() {
      if (!spies) return;
      var state = spies;
      spies = null;
      try { window.open = state.open; } catch (e) {}
      try { window.removeEventListener('message', state.onMessage, false); } catch (e) {}
      try { clearInterval(state.watch); } catch (e) {}
    }

    // GIS's own words, when it had any, with the level named. Errors are preferred over warnings rather
    // than merely "most recent": the button transport opt-out logs a deprecation notice on every single
    // flow, so quoting whichever line arrived last would staple that warning onto an unrelated origin
    // failure and send whoever reads it next chasing a red herring.
    function gsiComplaint() {
      if (!consoleTap) return '';
      if (consoleTap.last.error) return '; GIS said (error): ' + consoleTap.last.error;
      if (consoleTap.last.warn) return '; GIS said (warning): ' + consoleTap.last.warn;
      return '';
    }

    // GIS rejects a prompt whose calling origin is not in the OAuth client's Authorized JavaScript
    // origins, and the only way to fix that is to know the exact origin the embed is running under.
    // Carrying it in every diagnostic makes that a copy-paste instead of a hunt through DevTools.
    var origin = (window.location && window.location.origin) || 'unknown origin';

    // Always reached through window.google rather than the bare global: the bridge is loaded into
    // Unity's generated JS scope, and a qualified lookup cannot throw when GIS is absent.
    function gsi() {
      var g = window.google;
      return g && g.accounts && g.accounts.id ? g.accounts.id : null;
    }

    // Diagnostic channel: "<code>|<detail>". The code picks the sentence the player sees; the detail
    // is GIS's own wording and goes to the log for whoever is debugging this.
    //
    // Non-terminal by design - it informs C# without ending the flow, so a fallback notice can be
    // superseded by a later outcome. Once the flow is over it says nothing at all: a late async report
    // would otherwise overwrite the reason C# already handed to the player.
    function report(code, detail) {
      if (done) return;
      try {
        SendMessage(receiver, 'ReceiveDiagnostic', code + '|' + (detail || 'no reason given') + ' [origin: ' + origin + ']');
      } catch (e) {}
    }

    function removeOverlay() {
      if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
      overlay = null;
    }

    function finish(token, code, detail) {
      if (done) return;
      if (!token && code) report(code, detail);
      done = true;
      removeOverlay();
      removeConsoleTap();
      removeSpies();
      disarmStrandWarning();
      overlinkGoogleFlow = null;
      SendMessage(receiver, callback, token || '');
    }

    function reasonOf(notification, getter) {
      try { return (notification[getter] && notification[getter]()) || ''; } catch (e) { return ''; }
    }

    // Flattens GIS's error_callback payload, which has shipped with at least three shapes: a bare
    // {type}, a {type, details:{}}, and {type, metadata:{}} with the interesting text buried a level
    // down. Anything unrecognised is dumped rather than dropped, because the whole point of this
    // channel is that nobody has to guess which of them a given build got.
    function detailOf(err) {
      if (!err) return 'no error object';
      var nested = err.details || err.metadata || null;
      var parts = [];
      if (nested) {
        for (var key in nested) {
          if (!Object.prototype.hasOwnProperty.call(nested, key)) continue;
          var value = nested[key];
          if (value === null || value === undefined) continue;
          parts.push(key + '=' + (typeof value === 'object' ? JSON.stringify(value) : value));
        }
      }
      if (!parts.length && typeof err === 'object') {
        for (var top in err) {
          if (!Object.prototype.hasOwnProperty.call(err, top) || top === 'type') continue;
          parts.push(top + '=' + JSON.stringify(err[top]));
        }
      }
      return parts.length ? parts.join(', ') : 'no detail';
    }

    // The failure line and the escape hatch stay in the DOM but invisible until GIS gives a reason to
    // show them. Reserving the space up front would put a blank gap under a button that is working.
    function revealFailure(message) {
      if (failureLine) {
        failureLine.textContent = message;
        failureLine.style.display = 'block';
      }
      if (newTabButton) newTabButton.style.display = 'inline-block';
    }

    // The player must never be stranded by a silence. Every other way this flow opens up is triggered by
    // GIS saying something - error_callback, a dismissed prompt, a popup that closed - and the itch.io run
    // of 2026-10-22 proved GIS can say nothing at all: Google's chooser opened, its postMessage handshake
    // arrived, and no credential ever followed, so the card sat there looking healthy until C# burned its
    // deadline three minutes later with no exit on screen. So the button arms this as well. Ninety seconds
    // is long enough that a slow trip through the chooser never trips it, and short enough that the way out
    // is visible before the deadline. Non-terminal on purpose: a credential that arrives late is still the
    // one we want, so this only ever adds an option, never removes the attempt.
    var STRAND_WARNING_MS = 90 * 1000;

    function disarmStrandWarning() {
      if (strandTimer) { clearTimeout(strandTimer); strandTimer = null; }
    }

    function armStrandWarning() {
      disarmStrandWarning();
      strandTimer = setTimeout(function () {
        strandTimer = null;
        if (done) return;
        report('fallback', 'no credential ' + (STRAND_WARNING_MS / 1000) +
          's after the button appeared; GIS has reported nothing');
        revealFailure('Google has not answered yet. It may still come back - or continue in a new tab, ' +
          'where this game is not embedded and Google sign-in works normally.');
      }, STRAND_WARNING_MS);
    }

    // A top-level tab is first-party: no embedder, no permissions-policy delegation, no third-party
    // storage partitioning, so One Tap, FedCM and the button all behave there. It is the one route that
    // still works when the iframe itself is the obstruction, which is a property of how itch.io embeds
    // the game and not something any amount of JS can negotiate. Costs the player a reload of the game,
    // which is cheaper than a sign-in that cannot complete.
    function openInNewTab() {
      var url = (window.location && window.location.href) || '';
      var opened = null;
      // The spies would otherwise report our own window.open as though GIS had opened a sign-in popup.
      if (spies) spies.muted = true;
      try {
        opened = window.open(url, '_blank');
      } catch (e) {
        opened = null;
      }
      if (spies) spies.muted = false;
      if (!opened) {
        finish('', 'unavailable',
          'the browser refused to open a new tab for this embedded frame; allow pop-ups for this page, ' +
          'or use itch.io\u2019s own New window control beside the game frame');
        return;
      }
      // Not a cancellation the player chose, but the closest honest code: this attempt is over, and the
      // sign-in continues in the window they are now looking at.
      finish('', 'cancelled', 'continued in a new tab');
    }

    // FedCM in a cross-origin frame is only available if the embedder delegates publickey-credentials-get
    // via allow=. itch.io's embed iframe does not, and when it is missing Google's chooser opens, shows
    // "Processing your request...", and closes itself with no token - which looks like a mystery from the
    // outside. Logging the state turns that into one line.
    //
    // Every path speaks, including the ones that cannot measure anything. The first itch.io run of this
    // probe printed nothing at all, because the permissions query rejects on a browser with no opinion
    // about the permission name and the rejection was swallowed - and a diagnostic that can vanish in
    // silence is worthless precisely in the round where it is the only evidence available.
    function probeFedCM() {
      function unmeasurable(why) {
        report('fallback', 'FedCM availability in this frame: not measurable (' + why +
          '), so this build cannot say whether the embedder delegates it');
      }
      try {
        if (!window.navigator || !window.navigator.permissions || !window.navigator.permissions.query) {
          unmeasurable('no permissions API');
          return;
        }
        var verdict = window.navigator.permissions.query({ name: 'publickey-credentials-get' });
        if (!verdict || !verdict.then) {
          unmeasurable('the query returned no promise');
          return;
        }
        verdict.then(
          function (status) {
            report('fallback', 'FedCM availability in this frame: publickey-credentials-get=' +
              ((status && status.state) || 'unknown'));
          },
          function (e) { unmeasurable('the query rejected: ' + ((e && e.message) || String(e)).slice(0, 80)); }
        );
      } catch (e) {
        unmeasurable(String((e && e.message) || e).slice(0, 80));
      }
    }

    // The fallback. A button is the only GIS surface that reliably works when the page is an iframe,
    // because the player clicks it themselves and the account chooser opens as a normal popup.
    function showButton() {
      if (done) return;
      var id = gsi();
      try { id.cancel(); } catch (e) {}

      probeFedCM();

      overlay = document.createElement('div');
      overlay.style.cssText = 'position:fixed;left:0;top:0;right:0;bottom:0;z-index:2147483647;' +
        'display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.62);' +
        'font-family:Roboto,Arial,sans-serif;';

      var card = document.createElement('div');
      card.style.cssText = 'background:#fff;border-radius:12px;padding:24px 28px;' +
        'display:flex;flex-direction:column;align-items:center;gap:16px;max-width:90vw;';

      var title = document.createElement('div');
      title.textContent = 'Continue with Google';
      title.style.cssText = 'color:#1f1f1f;font-size:16px;font-weight:700;';

      var host = document.createElement('div');
      host.id = 'overlink-gsi-button';

      failureLine = document.createElement('div');
      failureLine.id = 'overlink-gsi-failure';
      failureLine.style.cssText = 'display:none;color:#b3261e;font-size:12px;line-height:1.5;' +
        'max-width:320px;text-align:center;';

      var actions = document.createElement('div');
      actions.style.cssText = 'display:flex;gap:12px;align-items:center;';

      newTabButton = document.createElement('button');
      newTabButton.id = 'overlink-gsi-newtab';
      newTabButton.textContent = 'Sign in in a new tab';
      // Hidden until GIS fails. Offering it alongside a working button invites the player out of the
      // iframe, and back into the game, for no reason.
      newTabButton.style.cssText = 'display:none;background:none;border:1px solid #dadce0;border-radius:6px;' +
        'color:#1a73e8;font-size:13px;cursor:pointer;font-family:inherit;padding:6px 12px;';
      newTabButton.onclick = openInNewTab;

      var close = document.createElement('button');
      close.id = 'overlink-gsi-cancel';
      close.textContent = 'Cancel';
      close.style.cssText = 'background:none;border:0;color:#5f6368;font-size:13px;cursor:pointer;' +
        'font-family:inherit;padding:6px 8px;';
      close.onclick = function () { finish('', 'cancelled', 'the player dismissed the Google button'); };

      actions.appendChild(newTabButton);
      actions.appendChild(close);

      card.appendChild(title);
      card.appendChild(host);
      card.appendChild(failureLine);
      card.appendChild(actions);
      overlay.appendChild(card);
      // Clicking the dimmed backdrop dismisses too, but the card must not leak the click to the canvas.
      overlay.onclick = function (event) {
        if (event.target === overlay) finish('', 'cancelled', 'the player dismissed the Google prompt');
      };
      document.body.appendChild(overlay);
      // Armed from the moment the card exists, not from the moment the button paints: a player looking at
      // a Google heading with nothing under it is the same stranding, just earlier.
      armStrandWarning();

      try {
        id.renderButton(host, {
          theme: 'outline',
          size: 'large',
          text: 'signin_with',
          shape: 'rectangular',
          logo_alignment: 'left'
        });
      } catch (e) {
        finish('', 'unavailable', 'renderButton threw: ' + ((e && e.message) || String(e)) + gsiComplaint());
        return;
      }

      // renderButton does not throw when the OAuth client rejects this origin; it quietly draws nothing.
      // Without this check the player gets a card with a heading, an empty hole where the button should
      // be, and no way out until the C# deadline three minutes later: a worse experience than the bug
      // this replaced. Anything at all appearing in the host counts as rendered, so a slow load from a
      // correctly configured client is never mistaken for a rejection.
      var waited = 0;
      var poll = setInterval(function () {
        if (done) { clearInterval(poll); return; }
        if (host.firstChild) { clearInterval(poll); return; }
        waited += 500;
        if (waited < 5000) return;
        clearInterval(poll);
        finish('', 'unavailable',
          'Google rendered no sign-in button, which means this page\'s origin is not in the OAuth ' +
          'client\'s Authorized JavaScript origins' + gsiComplaint());
      }, 500);
    }

    function start() {
      var id = gsi();
      if (!id) {
        finish('', 'unavailable', 'the GIS library is not present');
        return;
      }

      id.initialize({
        client_id: clientId,
        nonce: nonce,
        // FedCM is the modern transport for the account picker, and it is the only route by which One
        // Tap could appear inside an iframe at all, but it is not sufficient on its own. FedCM in a
        // cross-origin frame requires the *embedder* to delegate publickey-credentials-get, and itch.io
        // does not, so on itch.io this still yields isNotDisplayed and the button below is what actually
        // runs. Worth having anyway: the same build played from a first-party tab gets the native picker
        // with no overlay at all.
        use_fedcm_for_prompt: true,
        // The opposite choice, deliberately. This is the fix for a chooser that opens, sits on
        // "Processing your request", and then closes itself with no token. With FedCM for the button,
        // Google's chooser completes the exchange by calling navigator.credentials.get() from GIS's
        // iframe inside this page; in a third-party frame that call is refused unless the embedder
        // delegates publickey-credentials-get, the promise rejects, and the chooser dismisses itself
        // having authenticated the player and thrown the result away. The legacy transport completes the
        // exchange in its own top-level popup, where none of that applies - which is precisely why the
        // button is the iframe-safe surface in the first place. Google is retiring this opt-out, so
        // expect it to stop being an option and let the new-tab escape hatch become the primary path on
        // this embed; error_callback and probeFedCM's log line say which world a build is in.
        use_fedcm_for_button: false,
        auto_select: false,
        cancel_on_tap_outside: true,
        callback: function (response) {
          var credential = response && response.credential;
          if (credential) finish(credential);
          else finish('', 'failed', 'GIS invoked the callback with no credential');
        },
        // GIS's private error channel, and the only place the button flow's own failures get named:
        // popup_blocked, popup_closed, idp_timeout, issued_failed, gep_error. Before this existed, a
        // chooser that opened and died silently left C# waiting out its three-minute deadline with the
        // player staring at a card that could no longer do anything.
        //
        // Deliberately non-terminal. Every one of these is a failed attempt rather than a failed
        // session, and the button on screen still works, so the card stays up, the reason is shown, and
        // the player either retries or takes the new-tab exit.
        error_callback: function (err) {
          var type = (err && err.type) || 'unknown';
          report('failed', 'Google chooser error: ' + type + ' (' + detailOf(err) + ')' + gsiComplaint());
          revealFailure('Google closed the sign-in window (' + type + '). Try again, or continue in a new tab.');
        }
      });

      id.prompt(function (notification) {
        if (notification.isNotDisplayed()) {
          // This is the itch.io case. Not a cancellation, so do not report one.
          report('fallback', 'One Tap not displayed: ' + (reasonOf(notification, 'getNotDisplayedReason') || 'unknown'));
          showButton();
          return;
        }
        if (notification.isSkippedMoment()) {
          var skipped = reasonOf(notification, 'getSkippedReason');
          if (skipped === 'user_cancel') {
            finish('', 'cancelled', 'the player dismissed One Tap');
            return;
          }
          report('fallback', 'One Tap skipped: ' + (skipped || 'unknown'));
          showButton();
          return;
        }
        if (notification.isDismissedMoment()) {
          var dismissed = reasonOf(notification, 'getDismissedReason');
          // credential_returned means the callback above owns the handoff; anything else is a dead end.
          if (dismissed !== 'credential_returned') {
            finish('', 'failed', 'One Tap dismissed: ' + (dismissed || 'unknown'));
          }
        }
      });
    }

    // Installed before GIS loads so the origin rejection, which happens during initialize, is captured.
    // The timer bounds the hook's lifetime independently of the flow: if GIS never answers, the tap
    // still comes off the console rather than leaking for the page's life.
    installConsoleTap();
    installSpies();
    setTimeout(function () { removeConsoleTap(); removeSpies(); }, 4 * 60 * 1000);
    overlinkGoogleFlow = { abort: function () { finish(''); } };

    if (gsi()) {
      start();
      return;
    }

    var script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.onload = start;
    // A blocked or offline load is the other way this flow dies silently, so name it explicitly.
    script.onerror = function () {
      finish('', 'unavailable', 'accounts.google.com/gsi/client failed to load' + gsiComplaint());
    };
    document.head.appendChild(script);
  },

  // Called when GoogleTokenProvider's deadline wins. C# has already abandoned its pending task, so this
  // is deliberately silent: it only tears down the browser-owned overlay and hooks. Sending the same
  // empty callback as every other terminal path lets the bridge keep one cleanup route.
  OverlinkGoogleSignInCancel: function () {
    if (overlinkGoogleFlow) overlinkGoogleFlow.abort();
  }
});
