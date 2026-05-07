#!/usr/bin/env bash
set -Eeuo pipefail

fail() {
  echo "MYSSH_STATUS=failed"
  echo "MYSSH_ERROR=$*" >&2
  exit 1
}

reload_sshd() {
  if command -v systemctl >/dev/null 2>&1; then
    systemctl reload sshd 2>/dev/null || systemctl reload ssh 2>/dev/null || true
  fi
  service sshd reload 2>/dev/null || service ssh reload 2>/dev/null || true
}

[ "$(id -u)" -eq 0 ] || fail "cleanup script must run as root or through sudo -E"
[ -n "${MYSSH_TOKEN:-}" ] || fail "MYSSH_TOKEN is required"

STATE_FILE="/var/lib/myssh/$MYSSH_TOKEN.env"
[ -f "$STATE_FILE" ] || fail "state file not found for token"

# shellcheck disable=SC1090
. "$STATE_FILE"

[ -n "${MYSSH_TEMP_PORT:-}" ] || fail "state did not contain MYSSH_TEMP_PORT"

rm -f "${MYSSH_TEMP_SNIPPET:-/etc/ssh/sshd_config.d/99-myssh-temp-$MYSSH_TOKEN.conf}"

if command -v ufw >/dev/null 2>&1; then
  ufw delete allow "$MYSSH_TEMP_PORT/tcp" >/dev/null 2>&1 || true
fi
if command -v firewall-cmd >/dev/null 2>&1; then
  firewall-cmd --remove-port="$MYSSH_TEMP_PORT/tcp" >/dev/null 2>&1 || true
  firewall-cmd --runtime-to-permanent >/dev/null 2>&1 || true
fi
if command -v iptables >/dev/null 2>&1; then
  iptables -D INPUT -p tcp --dport "$MYSSH_TEMP_PORT" -j ACCEPT >/dev/null 2>&1 || true
fi

sshd -t
reload_sshd

rm -f "$STATE_FILE"
echo "MYSSH_STATUS=temporary_entry_closed"
echo "MYSSH_TEMP_PORT=$MYSSH_TEMP_PORT"
