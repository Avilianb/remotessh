#!/usr/bin/env bash
set -Eeuo pipefail

fail() {
  echo "MYSSH_STATUS=failed"
  echo "MYSSH_ERROR=$*" >&2
  exit 1
}

need_root() {
  if [ "$(id -u)" -ne 0 ]; then
    fail "bootstrap script must run as root or through sudo -E"
  fi
}

reload_sshd() {
  if command -v systemctl >/dev/null 2>&1; then
    systemctl reload sshd 2>/dev/null || systemctl reload ssh 2>/dev/null || true
  fi
  service sshd reload 2>/dev/null || service ssh reload 2>/dev/null || true
}

restore_on_error() {
  local status=$?
  trap - ERR
  if [ -n "${AUTH_KEYS:-}" ] && [ -n "${AUTH_KEYS_BACKUP:-}" ] && [ -f "$AUTH_KEYS_BACKUP" ]; then
    cp "$AUTH_KEYS_BACKUP" "$AUTH_KEYS" || true
  fi
  if [ -n "${SSHD_BACKUP:-}" ] && [ -f "$SSHD_BACKUP" ]; then
    cp "$SSHD_BACKUP" /etc/ssh/sshd_config || true
  fi
  if [ -n "${TEMP_SNIPPET:-}" ]; then
    rm -f "$TEMP_SNIPPET" || true
  fi
  reload_sshd
  echo "MYSSH_STATUS=restored_after_failure"
  exit "$status"
}

need_root
trap restore_on_error ERR

[ -n "${MYSSH_PUBLIC_KEY_B64:-}" ] || fail "MYSSH_PUBLIC_KEY_B64 is required"
[ -n "${MYSSH_TOKEN:-}" ] || fail "MYSSH_TOKEN is required"
[ -n "${MYSSH_TEMP_PORT:-}" ] || fail "MYSSH_TEMP_PORT is required"

case "$MYSSH_TEMP_PORT" in
  ''|*[!0-9]*) fail "MYSSH_TEMP_PORT must be numeric" ;;
esac

if [ "$MYSSH_TEMP_PORT" -lt 1024 ] || [ "$MYSSH_TEMP_PORT" -gt 65535 ]; then
  fail "MYSSH_TEMP_PORT must be between 1024 and 65535"
fi

PUBLIC_KEY="$(printf '%s' "$MYSSH_PUBLIC_KEY_B64" | base64 -d)"
case "$PUBLIC_KEY" in
  ssh-ed25519\ *) ;;
  *) fail "public key must be an ed25519 authorized_keys line" ;;
esac

TARGET_USER="${MYSSH_TARGET_USER:-${SUDO_USER:-root}}"
if [ "$TARGET_USER" = "root" ] && [ -n "${LOGNAME:-}" ] && [ "$LOGNAME" != "root" ]; then
  TARGET_USER="$LOGNAME"
fi

TARGET_HOME="$(getent passwd "$TARGET_USER" | cut -d: -f6)"
[ -n "$TARGET_HOME" ] || fail "could not resolve target user home"

TARGET_GROUP="$(id -gn "$TARGET_USER")"
SSH_DIR="$TARGET_HOME/.ssh"
AUTH_KEYS="$SSH_DIR/authorized_keys"
BACKUP_ROOT="/var/backups/myssh/$MYSSH_TOKEN"
STATE_DIR="/var/lib/myssh"
STATE_FILE="$STATE_DIR/$MYSSH_TOKEN.env"
SSHD_BACKUP="$BACKUP_ROOT/sshd_config"
AUTH_KEYS_BACKUP="$BACKUP_ROOT/authorized_keys"
TEMP_SNIPPET="/etc/ssh/sshd_config.d/99-myssh-temp-$MYSSH_TOKEN.conf"

mkdir -p "$BACKUP_ROOT" "$STATE_DIR" /etc/ssh/sshd_config.d
chmod 700 "$BACKUP_ROOT" "$STATE_DIR"

install -d -m 700 -o "$TARGET_USER" -g "$TARGET_GROUP" "$SSH_DIR"
if [ -f "$AUTH_KEYS" ]; then
  cp "$AUTH_KEYS" "$AUTH_KEYS_BACKUP"
else
  : > "$AUTH_KEYS_BACKUP"
fi
cp /etc/ssh/sshd_config "$SSHD_BACKUP"

printf '%s\n' "$PUBLIC_KEY" > "$AUTH_KEYS"
chown "$TARGET_USER:$TARGET_GROUP" "$AUTH_KEYS"
chmod 600 "$AUTH_KEYS"

EXISTING_PORTS="$(sshd -T 2>/dev/null | awk '/^port / {print $2}' | sort -n | uniq || true)"
if [ -z "$EXISTING_PORTS" ]; then
  EXISTING_PORTS="22"
fi

{
  echo "# Managed by My SSH token $MYSSH_TOKEN"
  for port in $EXISTING_PORTS; do
    echo "Port $port"
  done
  echo "Port $MYSSH_TEMP_PORT"
} > "$TEMP_SNIPPET"
chmod 644 "$TEMP_SNIPPET"

sshd -t

if command -v ufw >/dev/null 2>&1; then
  ufw allow "$MYSSH_TEMP_PORT/tcp" >/dev/null 2>&1 || true
fi
if command -v firewall-cmd >/dev/null 2>&1; then
  firewall-cmd --add-port="$MYSSH_TEMP_PORT/tcp" >/dev/null 2>&1 || true
  firewall-cmd --runtime-to-permanent >/dev/null 2>&1 || true
fi
if command -v iptables >/dev/null 2>&1; then
  iptables -C INPUT -p tcp --dport "$MYSSH_TEMP_PORT" -j ACCEPT >/dev/null 2>&1 ||
    iptables -I INPUT -p tcp --dport "$MYSSH_TEMP_PORT" -j ACCEPT >/dev/null 2>&1 || true
fi

reload_sshd

cat > "$STATE_FILE" <<EOF
MYSSH_TOKEN='$MYSSH_TOKEN'
MYSSH_TEMP_PORT='$MYSSH_TEMP_PORT'
MYSSH_TARGET_USER='$TARGET_USER'
MYSSH_TEMP_SNIPPET='$TEMP_SNIPPET'
MYSSH_BACKUP_ROOT='$BACKUP_ROOT'
EOF
chmod 600 "$STATE_FILE"

trap - ERR
echo "MYSSH_STATUS=ready"
echo "MYSSH_TOKEN=$MYSSH_TOKEN"
echo "MYSSH_TEMP_PORT=$MYSSH_TEMP_PORT"
echo "MYSSH_TARGET_USER=$TARGET_USER"
echo "MYSSH_BACKUP_ROOT=$BACKUP_ROOT"
