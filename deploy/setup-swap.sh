#!/usr/bin/env bash
# Gives the VPS a swap file (2 GB by default) as a safety net beside SQL Server's 2 GB cap.
# Run once on the server as root: sudo deploy/setup-swap.sh [size, e.g. 2G]
# Safe to re-run: it does nothing when the swap file is already active.
set -euo pipefail

SWAP_FILE=/swapfile
SIZE="${1:-2G}"

if [[ $EUID -ne 0 ]]; then
  echo "Run as root: sudo $0 $*" >&2
  exit 1
fi

if swapon --show=NAME --noheadings | grep -qx "$SWAP_FILE"; then
  echo "$SWAP_FILE is already active:"
  swapon --show
  exit 0
fi

if [[ ! -f $SWAP_FILE ]]; then
  fallocate -l "$SIZE" "$SWAP_FILE"
  chmod 600 "$SWAP_FILE"
  mkswap "$SWAP_FILE"
fi
swapon "$SWAP_FILE"

# Turn it on again after a reboot.
grep -q "^$SWAP_FILE " /etc/fstab || echo "$SWAP_FILE none swap sw 0 0" >> /etc/fstab

# Swap only under real memory pressure, not as a matter of course.
echo 'vm.swappiness=10' > /etc/sysctl.d/90-swappiness.conf
sysctl -q -p /etc/sysctl.d/90-swappiness.conf

swapon --show
