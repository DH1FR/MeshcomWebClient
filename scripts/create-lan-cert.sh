#!/usr/bin/env bash
# create-lan-cert.sh
# Erstellt ein selbstsigniertes HTTPS-Zertifikat fuer den LAN-Zugriff (Linux / Docker)
#
# Benoetigt: openssl  (sudo apt-get install -y openssl)
#
# Verwendung:
#   chmod +x scripts/create-lan-cert.sh
#   ./scripts/create-lan-cert.sh             # IP automatisch erkennen
#   ./scripts/create-lan-cert.sh 192.168.1.100  # IP manuell angeben

set -e

LAN_IP="${1:-}"
CERT_PASSWORD="meshcom2025"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
CERTS_DIR="$PROJECT_ROOT/MeshcomWebDesk/certs"

mkdir -p "$CERTS_DIR"

# ── Auto-Erkennung der LAN-IP ────────────────────────────────────────────────
if [ -z "$LAN_IP" ]; then
    LAN_IP=$(ip route get 1.1.1.1 2>/dev/null \
             | awk '{for(i=1;i<=NF;i++) if($i=="src") print $(i+1); exit}')
    if [ -z "$LAN_IP" ]; then
        LAN_IP=$(hostname -I 2>/dev/null | awk '{print $1}')
    fi
    if [ -z "$LAN_IP" ]; then
        echo "FEHLER: LAN-IP konnte nicht erkannt werden."
        echo "Bitte manuell angeben: $0 192.168.1.100"
        exit 1
    fi
    echo "LAN-IP automatisch erkannt: $LAN_IP"
fi

PFX_PATH="$CERTS_DIR/meshcom-lan.pfx"
CRT_PATH="$CERTS_DIR/meshcom-lan.crt"
KEY_PATH="$CERTS_DIR/meshcom-lan.key"
TMP_CNF=$(mktemp)

# ── OpenSSL-Konfiguration mit IP-SAN ─────────────────────────────────────────
cat > "$TMP_CNF" << EOF
[req]
default_bits       = 2048
prompt             = no
default_md         = sha256
req_extensions     = req_ext
x509_extensions    = v3_ca
distinguished_name = req_dn

[req_dn]
CN = MeshCom WebDesk

[req_ext]
subjectAltName = @alt_names

[v3_ca]
subjectAltName = @alt_names
basicConstraints = critical, CA:TRUE
keyUsage = critical, digitalSignature, keyEncipherment

[alt_names]
IP.1  = $LAN_IP
IP.2  = 127.0.0.1
DNS.1 = localhost
DNS.2 = meshcom.local
EOF

echo ""
echo "Erzeuge selbstsigniertes Zertifikat..."
echo "  LAN-IP   : $LAN_IP"
echo "  Gueltig  : 5 Jahre"
echo "  PFX      : $PFX_PATH"
echo "  CRT      : $CRT_PATH"

# ── Zertifikat + Schluessel erzeugen ─────────────────────────────────────────
openssl req -x509 -newkey rsa:2048 -sha256 -days 1825 -nodes \
    -keyout "$KEY_PATH" \
    -out    "$CRT_PATH" \
    -config "$TMP_CNF" 2>/dev/null

rm -f "$TMP_CNF"

# ── Als PFX exportieren (fuer Kestrel) ───────────────────────────────────────
openssl pkcs12 -export \
    -in    "$CRT_PATH" \
    -inkey "$KEY_PATH" \
    -out   "$PFX_PATH" \
    -passout "pass:$CERT_PASSWORD" 2>/dev/null

# Privater Schluessel im PFX enthalten – .key entfernen
rm -f "$KEY_PATH"

echo ""
echo "ZERTIFIKAT ERSTELLT"
echo "==================="
echo "  PFX (Kestrel)   : $PFX_PATH"
echo "  CRT (Geraete)   : $CRT_PATH"
echo ""
echo "Docker Compose – HTTPS aktivieren:"
echo "  environment:"
echo "    - ASPNETCORE_ENVIRONMENT=LanHttps"
echo "  volumes:"
echo "    - ./MeshcomWebDesk/certs:/app/certs:ro"
echo ""
echo "App starten:"
echo "  docker compose up -d --build"
echo ""
echo "Mobilgeraete:"
echo "  $CRT_PATH auf das Geraet kopieren"
echo "  Als CA-Zertifikat / vertrauenswuerdige Stammzertifizierungsstelle installieren"
echo ""
echo "  HTTP  -> http://$LAN_IP:5162"
echo "  HTTPS -> https://$LAN_IP:5163"
