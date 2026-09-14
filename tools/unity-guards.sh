#!/usr/bin/env bash
# Checks to run before any batch Unity invocation on this project. Source it, then call:
#
#   unity_guards "[tag]"
#
# Processes are found by their exact name (pgrep -x), never by searching command lines. A command-line
# search matched shells whose command text merely contained "Unity.app/Contents/MacOS/Unity" and
# refused runs for editors that did not exist — and a search for "VBCSCompiler" could have matched,
# and killed, the very shell running the check.
#
# Two failures this project has actually hit:
#
# 1. A second Unity process on the same project fights over Library/ (editors on other projects are
#    ignored). A batch run that has just
#    finished can take several seconds to exit, so wait up to 20 seconds before refusing; an editor
#    someone actually has open is still there afterwards and is still refused.
#
# 2. A VBCSCompiler server from a different .NET SDK answers Unity's csc and fails the build with
#    CS1504 "Method not found ... EncodingExtensions" on files nobody changed. Such a server runs as a
#    process named VBCSCompiler; Unity's own server runs as "dotnet exec …/VBCSCompiler.dll" and is
#    never matched. A foreign server is stopped; it restarts on demand for whatever started it.
# Unity processes working on THIS project. Another project's editor holds its own Library/ and is no
# conflict: refusing every Unity on the machine blocked runs while an unrelated project was open.
# Matched on the -projectPath argument of processes found by exact name, so no shell text is searched.
unity_on_this_project() {
  local root="${ROOT:-$(pwd)}" pid found=""
  for pid in $(pgrep -x Unity 2>/dev/null); do
    if ps -o command= -p "$pid" 2>/dev/null | grep -qiF -- "$root"; then
      found="$found $pid"
    fi
  done
  echo "$found"
}

unity_guards() {
  local tag="${1:-[unity]}"

  local waited=0
  while [ -n "$(unity_on_this_project)" ]; do
    if [ "$waited" -ge 20 ]; then
      echo "$tag a Unity editor has this project open (pid$(unity_on_this_project)); close it first" >&2
      return 1
    fi
    sleep 1
    waited=$((waited + 1))
  done

  local pid path stopped=0
  for pid in $(pgrep -x VBCSCompiler 2>/dev/null); do
    path=$(ps -o command= -p "$pid" 2>/dev/null)
    case "$path" in
      */Unity.app/*) continue ;;
    esac
    echo "$tag stopping a compiler server from another .NET SDK (pid $pid)"
    kill "$pid" 2>/dev/null || true
    stopped=1
  done
  if [ "$stopped" = "1" ]; then
    sleep 3
    for pid in $(pgrep -x VBCSCompiler 2>/dev/null); do
      case "$(ps -o command= -p "$pid" 2>/dev/null)" in
        */Unity.app/*) ;;
        *) echo "$tag compiler server $pid is still running; stop it and retry" >&2; return 1 ;;
      esac
    done
  fi
  return 0
}
