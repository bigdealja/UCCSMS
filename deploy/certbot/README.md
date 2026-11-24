# TLS certificates

`nginx` expects a cert at `deploy/certs/fullchain.pem` and key at `deploy/certs/privkey.pem`.

## Option A — self-signed (quick start / internal only)
```bash
./gen-selfsigned-cert.sh your-host.example.edu
```
Browsers warn it is untrusted; fine for testing, not for production.

## Option B — Let's Encrypt (public hostname)
The nginx `:80` server serves `/.well-known/acme-challenge/` from `deploy/certbot/www`.
Issue a certificate with certbot in webroot mode, then copy the result into `deploy/certs`:
```bash
docker run --rm \
  -v "$PWD/certbot/www:/var/www/certbot" \
  -v "$PWD/certs:/out" \
  certbot/certbot certonly --webroot -w /var/www/certbot \
  -d your-host.example.edu --email ops@example.edu --agree-tos --no-eff-email
# then place fullchain.pem / privkey.pem from /etc/letsencrypt/live/... into deploy/certs
```
Renew on a schedule (certbot renew) and reload nginx (`docker compose exec proxy nginx -s reload`).

## Option C — institution PKI
Drop your CA-issued `fullchain.pem` (server cert + intermediates) and `privkey.pem` into `deploy/certs`.

`deploy/certs/` is git-ignored — keys never get committed.
