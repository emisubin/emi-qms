#!/usr/bin/env python3
"""Bind a tested Docker image to the immutable, attested OCI archive to publish.

Read-only: verifies blob digests, the sole linux/amd64 runtime's config and
uncompressed layer hashes. Attestation manifests remain in the original index.
"""
import gzip
import hashlib
import json
import re
import sys
import tarfile


def verify(archive_path, inspection_path, expected_digest):
    def digest(data):
        return "sha256:" + hashlib.sha256(data).hexdigest()

    if not re.fullmatch(r"sha256:[0-9a-f]{64}", expected_digest):
        raise ValueError("invalid_digest")
    with tarfile.open(archive_path, "r:*") as archive:
        members = {member.name: member for member in archive.getmembers()}
        if len(members) != len(archive.getmembers()):
            raise ValueError("duplicate_archive_member")

        def read(name):
            member = members[name]
            if not member.isfile():
                raise ValueError("invalid_blob_member")
            return archive.extractfile(member).read()

        def blob(descriptor):
            checksum = descriptor["digest"]
            if not re.fullmatch(r"sha256:[0-9a-f]{64}", checksum):
                raise ValueError("unsupported_digest")
            data = read("blobs/sha256/" + checksum.split(":")[1])
            if digest(data) != checksum or len(data) != descriptor["size"]:
                raise ValueError("blob_digest_mismatch")
            return data

        index_bytes = read("index.json")
        index = json.loads(index_bytes)
        if digest(index_bytes) == expected_digest:
            root = index
        else:
            entries = index["manifests"]
            if len(entries) != 1 or entries[0]["digest"] != expected_digest:
                raise ValueError("archive_root_mismatch")
            root = json.loads(blob(entries[0]))
        runtime = []
        visited = set()

        def walk(manifest):
            if "manifests" in manifest:
                for entry in manifest["manifests"]:
                    data = blob(entry)
                    if entry["digest"] not in visited:
                        visited.add(entry["digest"])
                        walk(json.loads(data))
                return
            configuration = json.loads(blob(manifest["config"]))
            layers = [blob(layer) for layer in manifest["layers"]]
            platform = (configuration.get("os"), configuration.get("architecture"))
            if platform == ("unknown", "unknown"):
                return  # BuildKit attestation; still verified and copied with --all.
            if platform != ("linux", "amd64"):
                raise ValueError("unexpected_runtime_platform")
            diff_ids = []
            for layer, data in zip(manifest["layers"], layers):
                media = layer["mediaType"]
                if media == "application/vnd.oci.image.layer.v1.tar+gzip":
                    data = gzip.decompress(data)
                elif media != "application/vnd.oci.image.layer.v1.tar":
                    raise ValueError("unsupported_runtime_compression")
                diff_ids.append(digest(data))
            if configuration["rootfs"]["diff_ids"] != diff_ids:
                raise ValueError("runtime_layer_mismatch")
            runtime.append((manifest["config"]["digest"], diff_ids))

        walk(root)
        if len(runtime) != 1:
            raise ValueError("runtime_count_mismatch")
    with open(inspection_path, encoding="utf-8") as source:
        inspections = json.load(source)
    if len(inspections) != 1:
        raise ValueError("inspection_count_mismatch")
    image = inspections[0]
    if (image["Os"], image["Architecture"]) != ("linux", "amd64"):
        raise ValueError("loaded_platform_mismatch")
    if (image["Id"], image["RootFS"]["Layers"]) != runtime[0]:
        raise ValueError("loaded_runtime_mismatch")


if __name__ == "__main__":
    try:
        if len(sys.argv) != 4:
            raise ValueError("invalid_arguments")
        verify(*sys.argv[1:])
    except (ValueError, KeyError, TypeError, OSError, tarfile.TarError):
        print("ociRuntimeBinding=FAILED", file=sys.stderr)
        sys.exit(1)
    print("ociRuntimeBinding=PASS")
