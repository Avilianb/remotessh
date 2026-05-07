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

[ "$(id -u)" -eq 0 ] || fail "disable-password script must run as root or through sudo -E"

BACKUP_ROOT="/var/backups/myssh/disable-password-$(date +%Y%m%d%H%M%S)"
SNIPPET="/etc/ssh/sshd_config.d/01-myssh-disable-password.conf"

mkdir -p "$BACKUP_ROOT" /etc/ssh/sshd_config.d
chmod 700 "$BACKUP_ROOT"
cp /etc/ssh/sshd_config "$BACKUP_ROOT/sshd_config"

if [ -f /etc/ssh/sshd_config.d/50-cloud-init.conf ]; then
  cp /etc/ssh/sshd_config.d/50-cloud-init.conf "$BACKUP_ROOT/50-cloud-init.conf"
  sed -i 's/^[[:space:]]*PasswordAuthentication[[:space:]].*/# My SSH disabled previous PasswordAuthentication directive/' /etc/ssh/sshd_config.d/50-cloud-init.conf
fi

cat > "$SNIPPET" <<'EOF'
# Managed by My SSH after key-based login verification.
PasswordAuthentication no
KbdInteractiveAuthentication no
ChallengeResponseAuthentication no
PubkeyAuthentication yes
EOF

chmod 644 "$SNIPPET"
sshd -t
reload_sshd

echo "MYSSH_STATUS=password_login_disabled"
echo "MYSSH_BACKUP_ROOT=$BACKUP_ROOT"
