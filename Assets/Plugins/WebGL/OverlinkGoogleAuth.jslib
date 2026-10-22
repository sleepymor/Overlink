// Google Identity Services bridge for the WebGL player.
//
// The first version of this file had exactly one strategy: One Tap via
// google.accounts.id.prompt(). That is the most constrained thing GIS does, because it assumes it can
// draw its own floating UI over the page, which a game embedded in a third-party iframe, like every
// itch.io web play, cannot allow. When prompt() declines, GIS reports a *reason*, and the old code
// threw that reason away and sent an empty token, which C# turned into "Google sign-in was cancelled."
// So the player saw a lie (nobody cancelled) and the developer saw nothing actionable.
//
// Three changes follow from that:
//   1. FedCM is opted in, so a first-party tab gets the browser's native account picker. Inside a
//      third-party iframe it is not enough on its own - FedCM needs the embedder to delegate
//      publickey-credentials-get, and itch.io does not - so expect One Tap to still decline there.
//   2. When One Tap cannot display, a real Sign in with Google button is rendered into a DOM overlay as
//      the fallback, because the button flow is user-initiated and does not depend on GIS being able to
//      paint over the top-level page. On itch.io this is the path that actually runs.
//   3. Failures carry a machine-readable code plus the raw GIS reason, split by C# into player-facing
//      text and a console log respectively.
//
// Nothing here is covered by Unity's test runner - a .jslib belongs to no assembly, so there is no
// Assets/Tests file that can reference it. Control flow is covered by Tools/WebGL/gsi-bridge-test.mjs
// (`node Tools/WebGL/gsi-bridge-test.mjs`), which stubs the browser and GIS and runs in a second.
// Keep that in step with any change below; the alternative is discovering a regression in a WebGL build.
mergeInto(LibraryManager.library, {
  OverlinkGoogleSignIn: function (clientIdPtr, noncePtr, receiverPtr, callbackPtr) {
    var clientId = UTF8ToString(clientIdPtr);
    var nonce = UTF8ToString(noncePtr);
    var receiver = UTF8ToString(receiverPtr);
    var callback = UTF8ToString(callbackPtr);

    var done = false;
    var overlay = null;

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
      var lastLine = '';
      consoleTap = { originals: originals, lastLine: function () { return lastLine; } };
      function tap(level) {
        return function () {
          try {
            var text = '';
            for (var i = 0; i < arguments.length; i++) {
              if (typeof arguments[i] === 'string') text += (text ? ' ' : '') + arguments[i];
            }
            if (text.indexOf('GSI_LOGGER') >= 0) lastLine = text;
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

    // GIS's own words, when it had any.
    function gsiComplaint() {
      return consoleTap && consoleTap.lastLine() ? '; GIS said: ' + consoleTap.lastLine() : '';
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
      SendMessage(receiver, callback, token || '');
    }

    function reasonOf(notification, getter) {
      try { return (notification[getter] && notification[getter]()) || ''; } catch (e) { return ''; }
    }

    // The fallback. A button is the only GIS surface that reliably works when the page is an iframe,
    // because the player clicks it themselves and the account chooser opens as a normal popup.
    function showButton() {
      if (done) return;
      var id = gsi();
      try { id.cancel(); } catch (e) {}

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

      var close = document.createElement('button');
      close.textContent = 'Cancel';
      close.style.cssText = 'background:none;border:0;color:#5f6368;font-size:13px;cursor:pointer;' +
        'font-family:inherit;padding:4px 8px;';
      close.onclick = function () { finish('', 'cancelled', 'the player dismissed the Google button'); };

      card.appendChild(title);
      card.appendChild(host);
      card.appendChild(close);
      overlay.appendChild(card);
      // Clicking the dimmed backdrop dismisses too, but the card must not leak the click to the canvas.
      overlay.onclick = function (event) {
        if (event.target === overlay) finish('', 'cancelled', 'the player dismissed the Google prompt');
      };
      document.body.appendChild(overlay);

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
        // cross-origin frame requires the *embedder* to delegate publickey-credentials-get via allow=,
        // and itch.io does not delegate it, so on itch.io this still yields isNotDisplayed and the
        // button below is what actually runs. It is worth having anyway: the same build played from a
        // first-party tab (or "open in new window") gets the native picker with no overlay at all, and
        // without this flag GIS uses a transport Google is retiring.
        use_fedcm_for_prompt: true,
        auto_select: false,
        cancel_on_tap_outside: true,
        callback: function (response) {
          var credential = response && response.credential;
          if (credential) finish(credential);
          else finish('', 'failed', 'GIS invoked the callback with no credential');
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
    setTimeout(removeConsoleTap, 4 * 60 * 1000);

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
  }
});
