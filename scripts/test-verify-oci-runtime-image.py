#!/usr/bin/env python3
"""Synthetic OCI/config/layer binding counterexamples; no Docker or network."""
import gzip
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("binding", Path(__file__).with_name("verify-oci-runtime-image.py"))
binding = importlib.util.module_from_spec(spec)
spec.loader.exec_module(binding)


class BindingTests(unittest.TestCase):
    def fixture(self, root):
        members = {}

        def add(value, media):
            data = value if isinstance(value, bytes) else json.dumps(value).encode()
            checksum = "sha256:" + hashlib.sha256(data).hexdigest()
            members["blobs/sha256/" + checksum.split(":")[1]] = data
            return {"digest": checksum, "size": len(data), "mediaType": media}

        content = b"synthetic filesystem layer"
        layer = add(gzip.compress(content), "application/vnd.oci.image.layer.v1.tar+gzip")
        diff = "sha256:" + hashlib.sha256(content).hexdigest()
        config = add({"os": "linux", "architecture": "amd64", "rootfs": {"type": "layers", "diff_ids": [diff]}}, "application/vnd.oci.image.config.v1+json")
        manifest = add({"config": config, "layers": [layer]}, "application/vnd.oci.image.manifest.v1+json")
        attestation_config = add({"os": "unknown", "architecture": "unknown"}, "application/vnd.oci.image.config.v1+json")
        attestation = add({"config": attestation_config, "layers": []}, "application/vnd.oci.image.manifest.v1+json")
        index = add({"manifests": [manifest, attestation]}, "application/vnd.oci.image.index.v1+json")
        members["index.json"] = json.dumps({"manifests": [index]}).encode()
        image = [{"Id": config["digest"], "Os": "linux", "Architecture": "amd64", "RootFS": {"Layers": [diff]}}]
        archive = root / "image.tar"
        inspection = root / "image.json"
        return members, image, archive, inspection, index["digest"], layer

    def test_binding_and_counterexamples(self):
        for scenario in ("valid", "wrong-id", "wrong-layers", "wrong-platform", "corrupt-layer", "wrong-root", "duplicate-member"):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory() as temporary:
                members, image, archive, inspection, digest, layer = self.fixture(Path(temporary))
                if scenario == "wrong-id": image[0]["Id"] = "sha256:" + "0" * 64
                if scenario == "wrong-layers": image[0]["RootFS"]["Layers"] = []
                if scenario == "wrong-platform": image[0]["Architecture"] = "arm64"
                if scenario == "corrupt-layer": members["blobs/sha256/" + layer["digest"].split(":")[1]] = b"corrupt"
                if scenario == "wrong-root": digest = "sha256:" + "0" * 64
                with tarfile.open(archive, "w") as output:
                    entries = list(members.items())
                    if scenario == "duplicate-member": entries.append(entries[0])
                    for name, data in entries:
                        info = tarfile.TarInfo(name); info.size = len(data)
                        output.addfile(info, io.BytesIO(data))
                inspection.write_text(json.dumps(image))
                if scenario == "valid": binding.verify(archive, inspection, digest)
                else:
                    with self.assertRaises(ValueError): binding.verify(archive, inspection, digest)


if __name__ == "__main__":
    unittest.main()
