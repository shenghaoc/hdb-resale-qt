#!/usr/bin/env bash
# Existing hdb-debian apt.conf points at signed official Debian trixie metadata.
# Downloads/extracts only; no apt install/update, sudo, or machine-wide writes.
set -euo pipefail
root="${M10_ROOT:-/workspace/shared/m10-maplibre}"
apt_config="${M10_APT_CONFIG:-/workspace/shared/hdb-debian/apt.conf}"
mkdir -p "$root/packages" "$root/sysroot"
cd "$root/packages"
apt-get -c "$apt_config" download \
 libicu-dev=76.1-4 libicu76=76.1-4 \
 zlib1g-dev=1:1.3.dfsg+really1.3.1-1+b1 zlib1g=1:1.3.dfsg+really1.3.1-1+b1
for package in *.deb; do dpkg-deb -x "$package" "$root/sysroot"; done
sha256sum *.deb
