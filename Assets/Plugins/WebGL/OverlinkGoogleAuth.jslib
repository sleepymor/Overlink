mergeInto(LibraryManager.library, {
  OverlinkGoogleSignIn: function(clientIdPtr, noncePtr, receiverPtr, callbackPtr) {
    var clientId = UTF8ToString(clientIdPtr);
    var nonce = UTF8ToString(noncePtr);
    var receiver = UTF8ToString(receiverPtr);
    var callback = UTF8ToString(callbackPtr);
    var finish = function(token) {
      SendMessage(receiver, callback, token || "");
    };

    var start = function() {
      if (!window.google || !google.accounts || !google.accounts.id) {
        finish("");
        return;
      }
      google.accounts.id.initialize({
        client_id: clientId,
        nonce: nonce,
        callback: function(response) { finish(response && response.credential); },
        auto_select: false,
        cancel_on_tap_outside: true
      });
      google.accounts.id.prompt(function(notification) {
        if (notification.isNotDisplayed() || notification.isSkippedMoment()) finish("");
      });
    };

    if (window.google && google.accounts && google.accounts.id) start();
    else {
      var script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      script.onload = start;
      script.onerror = function() { finish(""); };
      document.head.appendChild(script);
    }
  }
});
