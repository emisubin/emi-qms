import { describe, expect, it, vi } from "vitest";
vi.mock("../src/api", () => ({ fetchBlob: vi.fn(), fetchJson: vi.fn() }));
import { busbarApi, validateBusbarPhoto } from "../src/interiorBusbar";
import { fetchBlob, fetchJson } from "../src/api";
const file = (name: string, type: string, size = 4) => ({ name, type, size } as File);
describe("busbar photo input", () => {
  it.each([["a.JPG", "image/jpeg"], ["a.png", "image/png"], ["a.HEIC", ""], ["a.heif", "application/octet-stream"], ["a.webp", "image/webp"]])("accepts supported mobile input %s", (name, type) => {
    expect(validateBusbarPhoto(file(name, type))).toBeNull();
  });
  it("rejects empty, oversized and unsupported files before upload", async () => {
    expect(validateBusbarPhoto(file("a.jpg", "image/jpeg", 0))).toContain("빈 파일");
    expect(validateBusbarPhoto(file("a.jpg", "image/jpeg", 40 * 1024 * 1024))).toBeNull();
    expect(validateBusbarPhoto(file("a.jpg", "image/jpeg", 40 * 1024 * 1024 + 1))).toContain("40MiB");
    await expect(busbarApi.uploadPhoto("u", "p", "front", file("a.pdf", "application/pdf"), "w")).rejects.toThrow("사진을 선택");
    expect(fetchJson).not.toHaveBeenCalled();
  });
  it("requests browser compatible saved photo preview", () => {
    busbarApi.photo("u", "p", "front");
    expect(fetchBlob).toHaveBeenCalledWith("/api/interior-busbar/products/p/photos/front?preview=true", "u");
  });
});
