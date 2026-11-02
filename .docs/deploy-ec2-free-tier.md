# Triển khai Chốn Riêng lên AWS EC2 (Free Tier)

- Ngày: 2026-10-31
- Mục tiêu: đưa hệ thống (API + 3 web app) lên một máy EC2 miễn phí để giáo viên truy cập và kiểm thử qua Internet.
- Phạm vi: 1 máy EC2, Docker Compose, HTTPS miễn phí. Cơ sở dữ liệu giữ nguyên SQL Server đang dùng (site4now).

---

## 1. Đề xuất

### 1.1. Chọn gói máy

| Hạng mục | Đề xuất | Lý do |
| --- | --- | --- |
| Loại máy | **t3.micro** (x86, 2 vCPU, 1 GB RAM) | Thuộc Free Tier; dùng x86 để chạy được image build trên máy cá nhân (máy ARM như t4g sẽ **không** chạy được image amd64) |
| Hệ điều hành | Ubuntu Server 24.04 LTS | Ổn định, nhiều tài liệu, Docker hỗ trợ chính thức |
| Region | **ap-southeast-1 (Singapore)** | Gần Việt Nam nhất, độ trễ thấp |
| Ổ đĩa | 30 GB gp3 | Mức tối đa của Free Tier cho EBS |
| IP | Elastic IP (IP tĩnh) | Không đổi khi khởi động lại máy; trỏ tên miền vào |
| HTTPS | **Caddy** + tên miền miễn phí (DuckDNS) | Caddy tự xin và gia hạn chứng chỉ Let's Encrypt, không cần cấu hình certbot |
| CSDL | Giữ **SQL Server site4now** | SQL Server cần tối thiểu 2 GB RAM, không chạy nổi trên t3.micro |

> **Lưu ý Free Tier (kiểm tra lại trên AWS Console trước khi tạo):**
> - Tài khoản tạo **trước 15/07/2025**: 750 giờ/tháng t2.micro/t3.micro trong 12 tháng, 30 GB EBS, 750 giờ IPv4 công khai.
> - Tài khoản tạo **từ 15/07/2025**: gói *Free plan* dùng tín dụng (tối đa 200 USD, 6 tháng); t3.micro nằm trong danh sách được dùng.
> - Từ 02/2024 AWS tính phí **mọi IPv4 công khai** (kể cả Elastic IP đang gắn). Free Tier cũ có miễn phí 750 giờ; Free plan mới trừ vào tín dụng. Nhớ **giải phóng Elastic IP** khi không dùng nữa (script mục 4.5).

### 1.2. Điểm quan trọng nhất: **không build trên EC2**

Máy chỉ có 1 GB RAM. Build image .NET SDK + Node trên đó rất chậm và dễ hết RAM. Vì vậy:

1. **Build image trên máy cá nhân** (`docker compose build`).
2. **Đẩy image sang EC2** bằng `docker save | ssh | docker load` (miễn phí, không cần registry).
3. EC2 chỉ **chạy** container (API ~150–250 MB, nginx và Caddy vài chục MB).

Máy EC2 vẫn có thêm **2 GB swap** để an toàn khi cao điểm.

### 1.3. Việc cần làm trước khi deploy

- [ ] Tắt log debug trên trình duyệt hoặc chuyển sang mặc định tắt (hiện log đang bật mặc định).
- [ ] Tạo file **`.env.production`** riêng: `Jwt__SigningKey` **mới**, mật khẩu Admin mạnh, `ASPNETCORE_ENVIRONMENT=Production`. Không commit file này (đã được `.gitignore` chặn bởi mẫu `.env.*`).
- [ ] Xóa phần mật khẩu trong mục "Admin Account" của README.
- [ ] **Cho phép IP của EC2 truy cập SQL Server site4now** (Control Panel → MSSQL → Remote access / IP whitelist), nếu site4now có giới hạn IP.
- [ ] Đăng ký tên miền miễn phí tại https://www.duckdns.org (ví dụ `chonrieng.duckdns.org`) — trỏ về Elastic IP sau khi tạo máy.
- [ ] Chạy thử `ASPNETCORE_ENVIRONMENT=Production` ở máy cá nhân một lần (chế độ Production chưa được kiểm thử).

---

## 2. Thiết kế

### 2.1. Kiến trúc

```
                Internet (giáo viên, khách, nhân viên)
                              │  HTTPS 443 / HTTP 80
                              ▼
┌──────────────────── EC2 t3.micro · Ubuntu 24.04 · Elastic IP ───────────────────┐
│  Security Group: 22 (chỉ IP của bạn) · 80 · 443                                   │
│                                                                                  │
│   ┌────────────┐      ┌──────────────────────────────┐      ┌────────────────┐   │
│   │ caddy      │ ───▶ │ web (nginx)                  │ ───▶ │ api (.NET 10)  │   │
│   │ :80 :443   │      │  /        → Web Khách        │ /api │ :8080          │   │
│   │ HTTPS tự   │      │  /admin/  → Web Quản lý      │      │ chỉ 127.0.0.1  │   │
│   │ động       │      │  /pos/    → POS              │      │ (Swagger qua   │   │
│   └────────────┘      └──────────────────────────────┘      │  SSH tunnel)   │   │
│                         mạng Docker nội bộ                  └───────┬────────┘   │
└─────────────────────────────────────────────────────────────────────┼────────────┘
                                                                      │ TCP 1433
                                                                      ▼
                                                     SQL Server site4now (giữ nguyên)
```

- **Chỉ Caddy mở cổng ra Internet.** `web` và `api` nằm trong mạng Docker; API chỉ bind `127.0.0.1:8080` để xem Swagger qua SSH tunnel.
- Trình duyệt chỉ thấy **một tên miền** → không cần CORS, cookie/token an toàn hơn.
- Caddy tự cấp và gia hạn chứng chỉ HTTPS; nginx trong `web` giữ nguyên cấu hình hiện tại.

### 2.2. Thành phần và file

| Thành phần | Nơi chạy | File |
| --- | --- | --- |
| Tạo hạ tầng (key, Security Group, EC2, Elastic IP) | Máy cá nhân, AWS CLI | `01-create-ec2.sh` (mục 4.2) |
| Cài đặt máy (Docker, swap, múi giờ, xoay log) | EC2, tự chạy khi khởi tạo (user-data) | `bootstrap.sh` (mục 4.1) |
| Build + đẩy image + chạy | Máy cá nhân → EC2 qua SSH | `03-deploy.sh` (mục 4.3) |
| Cấu hình Production | EC2 `/opt/cafecr` | `compose.prod.yaml`, `Caddyfile` (script tự tạo) |
| Biến môi trường bí mật | EC2 `/opt/cafecr/.env` (chmod 600) | lấy từ `.env.production` ở máy bạn |
| Dọn dẹp | Máy cá nhân | `99-destroy.sh` (mục 4.5) |

### 2.3. Luồng triển khai

```
[1] Chuẩn bị .env.production + DuckDNS
[2] ./01-create-ec2.sh          → in ra IP + lệnh SSH (máy tự cài Docker ~3–5 phút)
[3] Trỏ DuckDNS về IP
[4] HOST=<ip> DOMAIN=<tên miền> ./03-deploy.sh
[5] Mở https://<tên miền>/  ·  /admin/  ·  /pos/
[6] Lần cập nhật sau: chỉ chạy lại bước [4]
```

### 2.4. Bảo mật

| Rủi ro | Biện pháp |
| --- | --- |
| Dò mật khẩu SSH | Chỉ dùng key; cổng 22 chỉ mở cho IP của bạn |
| Lộ API/Swagger | API bind `127.0.0.1`; xem Swagger bằng `ssh -L 8080:127.0.0.1:8080` |
| Truyền mật khẩu/token không mã hóa | HTTPS bắt buộc qua Caddy |
| Lộ bí mật | `.env` chỉ nằm trên EC2 (chmod 600), không commit; dùng JWT key mới |
| Metadata EC2 bị khai thác | Bắt buộc IMDSv2 (`HttpTokens=required`) |
| Lỗ hổng hệ điều hành | `unattended-upgrades` tự cài bản vá bảo mật |
| Đầy ổ đĩa vì log | Docker log xoay vòng 10 MB × 3 file |

---

## 3. Chuẩn bị máy cá nhân

```bash
# AWS CLI v2 (Linux)
curl -fsSL "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o awscliv2.zip
unzip -q awscliv2.zip && sudo ./aws/install && rm -rf aws awscliv2.zip

# Đăng nhập: tạo Access Key cho IAM user (không dùng root) tại IAM → Users → Security credentials
aws configure          # Region: ap-southeast-1, output: json
aws sts get-caller-identity   # kiểm tra đăng nhập thành công
```

Tạo thư mục chứa script (không commit key `.pem` và `.env.production`):

```bash
mkdir -p ~/cafecr-deploy && cd ~/cafecr-deploy
```

Lưu các script ở mục 4 vào thư mục này, rồi `chmod +x *.sh`.

---

## 4. Bash script

### 4.1. `bootstrap.sh` — tự chạy trên EC2 lần đầu (user-data)

```bash
#!/bin/bash
# Chạy với quyền root khi EC2 khởi tạo lần đầu. Log: /var/log/cloud-init-output.log
set -euxo pipefail
export DEBIAN_FRONTEND=noninteractive

# 2 GB swap: t3.micro chỉ có 1 GB RAM
if [ ! -f /swapfile ]; then
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi
echo 'vm.swappiness=10' > /etc/sysctl.d/99-swap.conf
sysctl --system

timedatectl set-timezone Asia/Ho_Chi_Minh

apt-get update
apt-get -y upgrade
apt-get install -y ca-certificates curl unattended-upgrades

# Docker Engine + Compose plugin từ kho chính thức của Docker
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Xoay vòng log container để không đầy ổ 30 GB
mkdir -p /etc/docker
cat > /etc/docker/daemon.json <<'EOF'
{ "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }
EOF
systemctl enable --now docker
systemctl restart docker
usermod -aG docker ubuntu

# Thư mục ứng dụng
mkdir -p /opt/cafecr
chown ubuntu:ubuntu /opt/cafecr

# Tự cài bản vá bảo mật
dpkg-reconfigure -f noninteractive unattended-upgrades

touch /opt/cafecr/.bootstrap-done
```

**Máy EC2 chỉ cần những gì script này cài:** swap, múi giờ, bản vá hệ điều hành, Docker Engine + Docker Compose, xoay vòng log và thư mục `/opt/cafecr`. **Không** cần cài .NET, Node.js hay nginx trên máy, vì tất cả đã nằm trong image Docker.

**Ba cách chạy script này:**

| Cách tạo EC2 | Cách chạy `bootstrap.sh` |
| --- | --- |
| Dùng `01-create-ec2.sh` (khuyến nghị) | Tự động: script truyền qua `--user-data` |
| Tạo bằng AWS Console | Ở bước *Launch instance → Advanced details → User data*, dán toàn bộ nội dung `bootstrap.sh` |
| Máy đã tạo sẵn, chưa cài gì | Chạy tay qua SSH (xem bên dưới) |

```bash
# Chạy tay trên máy đã có sẵn
scp -i cafecr-key.pem bootstrap.sh ubuntu@<IP>:~
ssh -i cafecr-key.pem ubuntu@<IP> 'sudo bash ~/bootstrap.sh'
ssh -i cafecr-key.pem ubuntu@<IP> 'exit'   # đăng nhập lại để nhóm docker có hiệu lực

# Kiểm tra cài đặt thành công
ssh -i cafecr-key.pem ubuntu@<IP> 'docker --version && docker compose version && free -h && timedatectl | grep "Time zone"'

# Khi chạy qua user-data: xem tiến trình / lỗi
ssh -i cafecr-key.pem ubuntu@<IP> 'sudo tail -n 50 /var/log/cloud-init-output.log'
```

Nếu tạo bằng Console, nhớ chọn: Ubuntu 24.04 (x86), t3.micro, ổ 30 GB gp3, Security Group mở 22 (chỉ *My IP*), 80, 443; sau đó cấp Elastic IP ở *EC2 → Elastic IPs → Allocate → Associate*.

### 4.2. `01-create-ec2.sh` — tạo hạ tầng (chạy trên máy cá nhân)

```bash
#!/usr/bin/env bash
# Tạo key pair, Security Group, EC2 t3.micro (Ubuntu 24.04) và Elastic IP.
# Chạy lại an toàn: tài nguyên đã có sẽ được dùng lại.
set -euo pipefail

REGION="${REGION:-ap-southeast-1}"
NAME="${NAME:-cafecr}"
INSTANCE_TYPE="${INSTANCE_TYPE:-t3.micro}"   # x86: chạy được image build trên máy cá nhân
KEY_NAME="${KEY_NAME:-cafecr-key}"
DISK_GB="${DISK_GB:-30}"
export AWS_DEFAULT_REGION="$REGION"

cd "$(dirname "$0")"
[ -f bootstrap.sh ] || { echo "Thiếu bootstrap.sh cùng thư mục"; exit 1; }

MY_IP="$(curl -fsS https://checkip.amazonaws.com | tr -d '\n')/32"
echo "IP của bạn (mở cổng SSH): $MY_IP"

# 1) Key pair (ed25519), lưu thành $KEY_NAME.pem
if ! aws ec2 describe-key-pairs --key-names "$KEY_NAME" >/dev/null 2>&1; then
  aws ec2 create-key-pair --key-name "$KEY_NAME" --key-type ed25519 \
    --query KeyMaterial --output text > "$KEY_NAME.pem"
  chmod 400 "$KEY_NAME.pem"
  echo "Đã tạo key: $KEY_NAME.pem (giữ kỹ, không commit)"
fi

# 2) Security Group: 22 chỉ từ IP của bạn, 80/443 công khai
VPC_ID=$(aws ec2 describe-vpcs --filters Name=isDefault,Values=true --query 'Vpcs[0].VpcId' --output text)
SG_ID=$(aws ec2 describe-security-groups \
  --filters Name=group-name,Values="$NAME-sg" Name=vpc-id,Values="$VPC_ID" \
  --query 'SecurityGroups[0].GroupId' --output text)
if [ "$SG_ID" = "None" ]; then
  SG_ID=$(aws ec2 create-security-group --group-name "$NAME-sg" \
    --description "Chon Rieng web (SSH from admin IP, HTTP/HTTPS public)" \
    --vpc-id "$VPC_ID" --query GroupId --output text)
  aws ec2 authorize-security-group-ingress --group-id "$SG_ID" --protocol tcp --port 22  --cidr "$MY_IP"
  aws ec2 authorize-security-group-ingress --group-id "$SG_ID" --protocol tcp --port 80  --cidr 0.0.0.0/0
  aws ec2 authorize-security-group-ingress --group-id "$SG_ID" --protocol tcp --port 443 --cidr 0.0.0.0/0
  echo "Đã tạo Security Group: $SG_ID"
fi

# 3) AMI Ubuntu 24.04 mới nhất (amd64) do Canonical công bố
AMI_ID=$(aws ssm get-parameter \
  --name /aws/service/canonical/ubuntu/server/24.04/stable/current/amd64/hvm/ebs-gp3/ami-id \
  --query Parameter.Value --output text)

# 4) EC2 (bỏ qua nếu đã có máy cùng tên đang chạy/dừng)
INSTANCE_ID=$(aws ec2 describe-instances \
  --filters Name=tag:Name,Values="$NAME" Name=instance-state-name,Values=pending,running,stopping,stopped \
  --query 'Reservations[0].Instances[0].InstanceId' --output text)
if [ "$INSTANCE_ID" = "None" ]; then
  INSTANCE_ID=$(aws ec2 run-instances \
    --image-id "$AMI_ID" \
    --instance-type "$INSTANCE_TYPE" \
    --key-name "$KEY_NAME" \
    --security-group-ids "$SG_ID" \
    --block-device-mappings "DeviceName=/dev/sda1,Ebs={VolumeSize=$DISK_GB,VolumeType=gp3,DeleteOnTermination=true,Encrypted=true}" \
    --metadata-options HttpTokens=required,HttpEndpoint=enabled \
    --credit-specification CpuCredits=standard \
    --user-data file://bootstrap.sh \
    --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$NAME}]" \
    --query 'Instances[0].InstanceId' --output text)
  echo "Đang khởi tạo EC2: $INSTANCE_ID"
fi
aws ec2 wait instance-running --instance-ids "$INSTANCE_ID"

# 5) Elastic IP (dùng lại nếu đã gắn)
PUBLIC_IP=$(aws ec2 describe-addresses --filters Name=instance-id,Values="$INSTANCE_ID" \
  --query 'Addresses[0].PublicIp' --output text)
if [ "$PUBLIC_IP" = "None" ]; then
  ALLOC_ID=$(aws ec2 allocate-address --domain vpc \
    --tag-specifications "ResourceType=elastic-ip,Tags=[{Key=Name,Value=$NAME}]" \
    --query AllocationId --output text)
  aws ec2 associate-address --instance-id "$INSTANCE_ID" --allocation-id "$ALLOC_ID" >/dev/null
  PUBLIC_IP=$(aws ec2 describe-addresses --allocation-ids "$ALLOC_ID" --query 'Addresses[0].PublicIp' --output text)
fi

cat <<EOF

✅ Xong.
  Instance : $INSTANCE_ID ($INSTANCE_TYPE, $REGION)
  IP tĩnh  : $PUBLIC_IP
  SSH      : ssh -i $KEY_NAME.pem ubuntu@$PUBLIC_IP

Máy đang tự cài Docker (3–5 phút). Kiểm tra:
  ssh -i $KEY_NAME.pem ubuntu@$PUBLIC_IP 'test -f /opt/cafecr/.bootstrap-done && docker --version'

Tiếp theo:
  1. Trỏ DuckDNS về $PUBLIC_IP
  2. HOST=$PUBLIC_IP DOMAIN=<tên-miền>.duckdns.org ./03-deploy.sh
EOF
```

### 4.3. `03-deploy.sh` — build, đẩy image và chạy (chạy trên máy cá nhân)

```bash
#!/usr/bin/env bash
# Build image trên máy cá nhân, đẩy sang EC2 qua SSH, chạy với cấu hình Production + HTTPS.
# Cập nhật phiên bản mới: chạy lại script này.
set -euo pipefail

HOST="${HOST:?Đặt HOST=<Elastic IP>}"
DOMAIN="${DOMAIN:?Đặt DOMAIN=<tên-miền>.duckdns.org (hoặc DOMAIN=:80 nếu chưa có tên miền)}"
REPO="${REPO:-$HOME/datn/project-cafe}"
KEY="${KEY:-$(dirname "$(realpath "$0")")/cafecr-key.pem}"
ENV_FILE="${ENV_FILE:-$REPO/.env.production}"
REMOTE_DIR=/opt/cafecr

SSH=(ssh -i "$KEY" -o StrictHostKeyChecking=accept-new "ubuntu@$HOST")
SCP=(scp -i "$KEY" -o StrictHostKeyChecking=accept-new)

[ -f "$ENV_FILE" ] || { echo "Thiếu $ENV_FILE (xem mục 1.3)"; exit 1; }
grep -q '^ASPNETCORE_ENVIRONMENT=Production' "$ENV_FILE" || echo "⚠️  Nên đặt ASPNETCORE_ENVIRONMENT=Production trong $ENV_FILE"

"${SSH[@]}" 'test -f /opt/cafecr/.bootstrap-done' || { echo "EC2 chưa cài xong Docker, đợi thêm vài phút."; exit 1; }

# 1) Build trên máy cá nhân (amd64, khớp với t3.micro)
cd "$REPO"
DOCKER_DEFAULT_PLATFORM=linux/amd64 docker compose build
GIT_SHA=$(git rev-parse --short HEAD)

# 2) Đẩy image qua SSH (không cần registry)
echo "Đang đẩy image sang EC2 (lần đầu ~1–3 phút)…"
docker save cafe-cr-api:latest cafe-cr-web:latest | gzip | "${SSH[@]}" 'gunzip | docker load'

# 3) Cấu hình Production: Caddy giữ cổng 80/443, web không còn mở cổng ra ngoài
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
cat > "$TMP/compose.prod.yaml" <<'EOF'
services:
  api:
    image: cafe-cr-api:latest
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
    mem_limit: 600m

  web:
    image: cafe-cr-web:latest
    ports: !reset []        # chỉ Caddy mở cổng ra Internet
    mem_limit: 128m

  caddy:
    image: caddy:2.8-alpine
    restart: unless-stopped
    depends_on:
      - web
    ports:
      - "80:80"
      - "443:443"
    environment:
      DOMAIN: ${DOMAIN}
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    mem_limit: 128m

volumes:
  caddy_data:
  caddy_config:
EOF
cat > "$TMP/Caddyfile" <<'EOF'
{$DOMAIN} {
    encode gzip
    reverse_proxy web:80
}
EOF

"${SCP[@]}" "$REPO/compose.yaml" "$TMP/compose.prod.yaml" "$TMP/Caddyfile" "ubuntu@$HOST:$REMOTE_DIR/"
"${SCP[@]}" "$ENV_FILE" "ubuntu@$HOST:$REMOTE_DIR/.env"

# 4) Chạy trên EC2
"${SSH[@]}" bash -s -- "$DOMAIN" "$GIT_SHA" <<'REMOTE'
set -euo pipefail
cd /opt/cafecr
chmod 600 .env
grep -q '^DOMAIN=' .env && sed -i "s|^DOMAIN=.*|DOMAIN=$1|" .env || echo "DOMAIN=$1" >> .env
docker tag cafe-cr-api:latest "cafe-cr-api:$2"
docker tag cafe-cr-web:latest "cafe-cr-web:$2"
docker compose -f compose.yaml -f compose.prod.yaml up -d --no-build --remove-orphans
docker image prune -f >/dev/null
docker compose -f compose.yaml -f compose.prod.yaml ps
REMOTE

# 5) Kiểm tra nhanh
SCHEME=https; [ "$DOMAIN" = ":80" ] && SCHEME=http && DOMAIN=$HOST
sleep 5
for path in / /admin/ /pos/ /api/branches; do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "$SCHEME://$DOMAIN$path" || true)
  echo "$path → $code"
done
echo "✅ Đã deploy phiên bản $GIT_SHA: $SCHEME://$DOMAIN/  ·  /admin/  ·  /pos/"
```

> Chưa có tên miền? Dùng `DOMAIN=:80` để chạy HTTP qua IP. Chỉ nên dùng để thử, vì mật khẩu và token sẽ đi qua mạng **không mã hóa**.

### 4.4. Vận hành hằng ngày

```bash
KEY=~/cafecr-deploy/cafecr-key.pem; HOST=<Elastic IP>
C="docker compose -f compose.yaml -f compose.prod.yaml"

# Xem log API / Caddy
ssh -i $KEY ubuntu@$HOST "cd /opt/cafecr && $C logs -f --tail 100 api"
ssh -i $KEY ubuntu@$HOST "cd /opt/cafecr && $C logs --tail 50 caddy"

# Khởi động lại toàn bộ
ssh -i $KEY ubuntu@$HOST "cd /opt/cafecr && $C restart"

# Xem Swagger (chỉ trên máy bạn): mở http://localhost:8080/swagger
ssh -i $KEY -N -L 8080:127.0.0.1:8080 ubuntu@$HOST

# Theo dõi RAM / swap
ssh -i $KEY ubuntu@$HOST 'free -h && docker stats --no-stream'

# Seed dữ liệu demo (CHỈ với CSDL mới; CSDL hiện tại đã có dữ liệu)
ssh -i $KEY ubuntu@$HOST "cd /opt/cafecr && $C run --rm -e ASPNETCORE_ENVIRONMENT=Development api --seed-development-data"

# Tạm dừng máy khi không demo (đỡ tốn giờ chạy; IPv4 vẫn bị tính phí theo mục 1.1)
aws ec2 stop-instances  --instance-ids <instance-id>
aws ec2 start-instances --instance-ids <instance-id>
```

Migration CSDL vẫn chạy từ **máy cá nhân** như hiện nay (`dotnet ef database update`), không chạy trên EC2.

### 4.5. `99-destroy.sh` — xóa toàn bộ để không phát sinh phí

```bash
#!/usr/bin/env bash
# Xóa EC2, Elastic IP, Security Group và key pair đã tạo bởi 01-create-ec2.sh.
set -euo pipefail
REGION="${REGION:-ap-southeast-1}"; NAME="${NAME:-cafecr}"; KEY_NAME="${KEY_NAME:-cafecr-key}"
export AWS_DEFAULT_REGION="$REGION"

read -r -p "Xóa toàn bộ tài nguyên '$NAME' ở $REGION? Gõ 'yes' để xác nhận: " ok
[ "$ok" = "yes" ] || exit 1

INSTANCE_ID=$(aws ec2 describe-instances --filters Name=tag:Name,Values="$NAME" \
  Name=instance-state-name,Values=pending,running,stopping,stopped \
  --query 'Reservations[0].Instances[0].InstanceId' --output text)

if [ "$INSTANCE_ID" != "None" ]; then
  for ALLOC in $(aws ec2 describe-addresses --filters Name=instance-id,Values="$INSTANCE_ID" --query 'Addresses[].AllocationId' --output text); do
    ASSOC=$(aws ec2 describe-addresses --allocation-ids "$ALLOC" --query 'Addresses[0].AssociationId' --output text)
    aws ec2 disassociate-address --association-id "$ASSOC"
    aws ec2 release-address --allocation-id "$ALLOC"
  done
  aws ec2 terminate-instances --instance-ids "$INSTANCE_ID" >/dev/null
  aws ec2 wait instance-terminated --instance-ids "$INSTANCE_ID"
fi

SG_ID=$(aws ec2 describe-security-groups --filters Name=group-name,Values="$NAME-sg" --query 'SecurityGroups[0].GroupId' --output text)
[ "$SG_ID" != "None" ] && aws ec2 delete-security-group --group-id "$SG_ID"
aws ec2 delete-key-pair --key-name "$KEY_NAME" || true
echo "✅ Đã xóa. Kiểm tra lại Billing → Free Tier usage."
```

### 4.6. (Khuyến nghị) Cảnh báo chi phí

```bash
# Gửi email khi chi phí dự kiến vượt 1 USD/tháng
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
aws budgets create-budget --account-id "$ACCOUNT_ID" \
  --budget '{"BudgetName":"cafecr-1usd","BudgetLimit":{"Amount":"1","Unit":"USD"},"TimeUnit":"MONTHLY","BudgetType":"COST"}' \
  --notifications-with-subscribers '[{"Notification":{"NotificationType":"FORECASTED","ComparisonOperator":"GREATER_THAN","Threshold":100},"Subscribers":[{"SubscriptionType":"EMAIL","Address":"<email-cua-ban>"}]}]'
```

---

## 5. Kiểm tra sau khi deploy

| Kiểm tra | Kết quả mong đợi |
| --- | --- |
| `https://<tên miền>/` | Web Khách, có ổ khóa HTTPS |
| `/admin/` đăng nhập `admin01.demo@cafecr.invalid` | Tổng quan có số liệu |
| `/pos/` đăng nhập `staff01.demo@cafecr.invalid` | Thấy ca hôm nay / sơ đồ chỗ |
| `/api/branches` | 200, danh sách 5 chi nhánh |
| `http://<IP>:8080` từ máy khác | **Không** kết nối được (API không lộ ra ngoài) |
| `ssh` từ mạng khác (ví dụ 4G) | **Bị chặn** (chỉ IP của bạn) |
| `free -h` trên EC2 | RAM dùng < 800 MB, swap gần như trống |

## 6. Sự cố thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
| --- | --- |
| SSH timeout sau khi đổi mạng | IP của bạn thay đổi → thêm IP mới: `aws ec2 authorize-security-group-ingress --group-id <sg> --protocol tcp --port 22 --cidr $(curl -s https://checkip.amazonaws.com)/32` |
| Caddy báo lỗi chứng chỉ | DuckDNS chưa trỏ đúng IP hoặc cổng 80 bị chặn; xem `logs caddy` |
| API khởi động lại liên tục | Sai `.env` (chuỗi kết nối, JWT key ngắn hơn 32 byte) hoặc site4now chặn IP EC2 → `logs api` |
| `exec format error` | Image build cho ARM; build lại với `DOCKER_DEFAULT_PLATFORM=linux/amd64` (script đã đặt) |
| Trang chậm / container bị kill | Hết RAM → kiểm tra `docker stats`; cân nhắc t3.small (ngoài Free Tier cũ) |

## 7. Hướng mở rộng (sau Free Tier)

- Đẩy image lên **GitHub Container Registry** + GitHub Actions để deploy tự động khi merge.
- Chuyển CSDL sang **Amazon RDS for SQL Server Express** (cùng VPC, sao lưu tự động).
- Kiến trúc trong tài liệu tham khảo: S3 + CloudFront cho frontend, ECS Fargate cho API, RDS — phù hợp khi cần mở rộng, nhưng **không miễn phí**.
