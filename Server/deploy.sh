#!/bin/zsh
# Puts the VR multiplayer server (server.js) on aiserver as the systemd user service block-tower-vr, port 3010, LAN
# only. ufw is on there: the port needs a rule, added by hand once:
#   sudo ufw allow from 192.168.0.0/24 to any port 3010 proto tcp comment 'BlockTower VR'
# Runs on the private Node in ~/block-tower/node (nothing installed system-wide). Safe to rerun.
set -euo pipefail
cd "${0:A:h}"
HOST=aiserver-lan
PORT=3010

node test.js
ssh "$HOST" test -x block-tower/node/bin/node || { echo "no ~/block-tower/node on $HOST"; exit 1; }
rsync -a --delete --exclude test.js --exclude bot.js --exclude deploy.sh ./ "$HOST:block-tower/vr/"

ssh "$HOST" PORT=$PORT 'bash -s' <<'EOF'
set -euo pipefail
U=~/.config/systemd/user
mkdir -p $U
cat > $U/block-tower-vr.service <<UNIT
[Unit]
Description=BlockTower VR multiplayer server (port $PORT, LAN only)
After=network-online.target

[Service]
WorkingDirectory=%h/block-tower/vr
Environment=PORT=$PORT
ExecStart=%h/block-tower/node/bin/node server.js
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
UNIT
systemctl --user daemon-reload
systemctl --user enable --now block-tower-vr
systemctl --user restart block-tower-vr
sleep 1
systemctl --user is-active block-tower-vr
EOF
