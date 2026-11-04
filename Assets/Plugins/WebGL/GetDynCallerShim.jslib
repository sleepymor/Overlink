// Shim for emscripten's getDynCaller, which Unity's WebGL WebSocket glue calls
// on ws.onopen / ws.onclose. In some Unity/emscripten patch combinations this
// global is missing, producing:
//   ReferenceError: getDynCaller is not defined
//   at instance.ws.onopen (...)
//   at instance.ws.onclose (...)
// Defining it here as a no-op-compatible fallback prevents the ReferenceError.
// If the real emscripten runtime already defines it, this assignment is a no-op
// because the variable already exists in scope.
if (typeof getDynCaller === 'undefined') {
  getDynCaller = function (func, sig) {
    if (typeof func === 'function') {
      return function () {
        return func.apply(null, arguments);
      };
    }
    // func is a function pointer (number). Fall back to dynCall if available.
    return function () {
      if (typeof Runtime !== 'undefined' && typeof Runtime.dynCall === 'function') {
        return Runtime.dynCall(sig || 'vi', func, Array.prototype.slice.call(arguments));
      }
    };
  };
}
