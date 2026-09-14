#!/usr/bin/env bash
# Checks to run before any batch Unity invocation on this project. Source it, then call:
#
#   unity_guards "[tag]"
#
# Two failures this project has actually hit:
#
# 1. A second Unity process on the same project fights over Library/. The process list is read with
#    a bracketed pattern and shells are excluded: `pgrep -f` also matches any shell whose command line
#    merely contains the text, and once refused a build for an editor that did not exist.
#
# 2. A VBCSCompiler server from a different .NET SDK — anything outside Unity.app — answers Unity's
#    csc and fails the build with CS1504 "Method not found ... EncodingExtensions" on files nobody
#    changed. Such a server is stopped; it restarts on demand for whatever started it. Unity's own
#    compiler server is left alone.
unity_guards() {
  local tag="${1:-[unity]}"

  if ps -axo command= | grep -E '[/]Unity\.app/Contents/MacOS/Unity' | grep -vqE 'zsh|bash'; then
    echo "$tag another Unity process is running; close it first" >&2
    return 1
  fi

  local foreign
  foreign=$(ps -axo pid=,command= | grep '[V]BCSCompiler' | grep -v '/Unity.app/' | awk '{print $1}' || true)
  if [ -n "$foreign" ]; then
    echo "$tag stopping a compiler server from another .NET SDK (pid $foreign)"
    echo "$foreign" | xargs kill 2>/dev/null || true
    sleep 3
    if ps -axo command= | grep '[V]BCSCompiler' | grep -vq '/Unity.app/'; then
      echo "$tag that compiler server is still running; stop it and retry" >&2
      return 1
    fi
  fi
}
