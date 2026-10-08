#!/bin/sh
# Install a built package in a clean container and prove the installed application works.
#
#   verify_installed.sh <deb|rpm> <package-file> <repo-tools-root> <report-dir>
#
# Runs as root inside a pristine debian:13 / fedora:43 style container. The phases are ordered so the
# package manager alone supplies the package's dependencies: test tooling (Python, Xvfb, strace) is added
# only AFTER the dependency audit, so it cannot mask a missing dependency declaration.
set -eu
format=$1 package=$2 work=$3 out=$4
root=/opt/hdb-resale-explorer
mkdir -p "$out"
chmod 0777 "$out"
cp /etc/os-release "$out/os-release.txt"
log() { printf '\n== %s\n' "$*"; }
fail() { printf 'VERIFY FAILED: %s\n' "$*" >&2; exit 1; }

log "phase 0: the build environment is not present"
for path in /opt/Qt /usr/share/dotnet /usr/lib/dotnet /root/.nuget /root/.dotnet /usr/lib/qt6 /usr/lib64/qt6; do
  [ ! -e "$path" ] || fail "$path exists in the clean container"
done

log "phase 1: install only the package"
case $format in
  deb) export DEBIAN_FRONTEND=noninteractive
       apt-get update -q
       apt-get install -y -q "$package" ;;
  rpm) dnf install -y -q "$package" ;;
  *) fail "unknown format $format" ;;
esac
[ -x /usr/bin/hdb-resale-explorer ] || fail "/usr/bin/hdb-resale-explorer is missing or not executable"

log "phase 2: every packaged ELF resolves with only the declared dependencies installed"
elf_magic=$(printf '\177ELF')
checked=0
unresolved=""
find "$root" -type f | while read -r file; do
  [ "$(head -c4 "$file")" = "$elf_magic" ] || continue
  printf '%s\n' "$file"
done > "$out/elf-files.txt"
while read -r file; do
  checked=$((checked + 1))
  missing=$(LD_LIBRARY_PATH="$root/qt/lib" ldd "$file" 2>&1 | grep 'not found' || true)
  [ -z "$missing" ] && continue
  # The one accepted gap: Microsoft's optional LTTng tracing provider (see stage_linux.py).
  case $file:$missing in
    "$root/dotnet/shared/Microsoft.NETCore.App/"*"/libcoreclrtraceptprovider.so":*"liblttng-ust.so.0 => not found") ;;
    *) unresolved="$unresolved\n$file: $missing" ;;
  esac
done < "$out/elf-files.txt"
[ -z "$unresolved" ] || fail "unresolved libraries:$unresolved"
echo "$(wc -l < "$out/elf-files.txt") ELF files audited"
grep -v '^#' "$root/system-libraries.txt" | while read -r library; do
  ldconfig -p | grep -q "^[[:space:]]*$library " || fail "dlopen/declared library $library is not installed"
done

log "phase 3: add test tooling"
case $format in
  deb) apt-get install -y -q python3 xvfb xauth strace desktop-file-utils appstream util-linux ;;
  rpm) dnf install -y -q python3 xorg-x11-server-Xvfb xauth strace desktop-file-utils appstream util-linux ;;
esac
id hdbtest >/dev/null 2>&1 || useradd --create-home hdbtest

log "phase 4: package-manager facts and desktop integration"
python3 "$work/tools/package/verify_installed.py" "$format" --packages "$(dirname "$package")" --report "$out/installed-facts.json"

log "phase 5: recorded-API launch through the installed launcher, as an unprivileged user on X11"
runuser -u hdbtest -- sh -c 'Xvfb :99 -screen 0 1280x800x24 >/dev/null 2>&1 &'
sleep 2
launch() { runuser -u hdbtest -- env DISPLAY=:99 python3 "$work/tools/package/launch_check.py" \
  --package "$root" --launcher /usr/bin/hdb-resale-explorer --forbid /opt/Qt --forbid /home/runner \
  --forbid "$work/src" --forbid "$work/tests/HdbResale.Tests" --trace-file-access "$@"; }
launch --log "$out/launch.log" --report "$out/launch-report.json"

log "phase 6: negative control: an unreachable API must fail the same check"
if launch --api-base-url http://127.0.0.1:9/ --timeout 30 --log "$out/negative-launch.log" --report "$out/negative-report.json"; then
  fail "the launch check passed against an unreachable API; it cannot detect failures"
fi

log "phase 7: uninstall removes everything the package installed"
case $format in
  deb) apt-get remove -y -q hdb-resale-explorer ;;
  rpm) dnf remove -y -q hdb-resale-explorer ;;
esac
python3 "$work/tools/package/verify_installed.py" "$format" --removed
log "installed package verification passed"
