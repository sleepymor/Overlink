// Shim for emscripten's getDynCaller, which Unity's WebGL WebSocket glue calls
// on ws.onopen / ws.onclose. In some Unity/emscripten patch combinations this
// global is missing, producing:
//
//   ReferenceError: getDynCaller is not defined
//   at instance.ws.onopen (...)
//   at instance.ws.onclose (...)
//
// Unity's generated glue calls it as getDynCaller(signature, functionPointer).
// Defining it here via mergeInto prevents the ReferenceError.

var getDynCallerShim = function (sig, func) {
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

mergeInto(LibraryManager.library, {
  getDynCaller: getDynCallerShim
});

// Expose as globals so Unity's generated WebSocket glue can find it.
// The glue calls bare getDynCaller(...) as a global, not Module.getDynCaller.
// ProjectSettings.webGLEmscriptenArgs also exports it via EXPORTED_RUNTIME_METHODS,
// but we set both Module.getDynCaller and window.getDynCaller here for belt and suspenders.
if (typeof Module !== 'undefined' && typeof Module.getDynCaller === 'undefined') {
  Module.getDynCaller = getDynCallerShim;
}
if (typeof window !== 'undefined' && typeof window.getDynCaller === 'undefined') {
  window.getDynCaller = getDynCallerShim;
}
