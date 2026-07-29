#!/bin/sh
set -eu

certificate=/run/secrets/tls_certificate
private_key=/run/secrets/tls_private_key

test -n "${PUBLIC_HOST:-}"
test -r "${certificate}"
test -r "${private_key}"

openssl x509 -in "${certificate}" -noout -checkend 2592000 >/dev/null
openssl x509 -in "${certificate}" -noout -checkhost "${PUBLIC_HOST}" >/dev/null

certificate_public_key="$(
  openssl x509 -in "${certificate}" -pubkey -noout \
    | openssl pkey -pubin -outform DER 2>/dev/null \
    | sha256sum \
    | cut -d ' ' -f 1
)"
private_public_key="$(
  openssl pkey -in "${private_key}" -pubout -outform DER 2>/dev/null \
    | sha256sum \
    | cut -d ' ' -f 1
)"

test -n "${certificate_public_key}"
test "${certificate_public_key}" = "${private_public_key}"
