import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { BusbarMobilePhotoWorkspace } from "../src/BusbarMobilePhotoWorkspace";
import { busbarApi, type BusbarProduct } from "../src/interiorBusbar";

const draftProduct: BusbarProduct = {
  id: "panel-1",
  productFamilyId: "family-1",
  planId: "plan-1",
  planSequence: 1,
  workerId: null,
  workerName: null,
  status: "Draft",
  hasFront: false,
  hasBack: false,
  publicationState: "Published",
  revision: 1,
  publishedRevision: 1,
};
const workers = [{ id: "worker-1", code: "W1", name: "작업자 A", isActive: true }];

beforeEach(() => {
  vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:preview");
  vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => undefined);
  vi.spyOn(busbarApi, "uploadPhoto").mockResolvedValue({ id: "panel-1" });
  vi.spyOn(busbarApi, "photo").mockResolvedValue(new Blob(["photo"]));
  vi.spyOn(busbarApi, "previewPhoto").mockResolvedValue(new Blob(["preview"], { type: "image/jpeg" }));
});
afterEach(() => vi.restoreAllMocks());

describe("mobile busbar photo workflow", () => {
  it("keeps a selected photo as a draft until the explicit save action", async () => {
    const run = vi.fn(async (action: () => Promise<unknown>) => { await action(); return true; });
    render(<BusbarMobilePhotoWorkspace user="user" product={draftProduct} workers={workers} canWrite busy={false} run={run} />);

    fireEvent.change(screen.getByLabelText("실제 제조 작업자"), { target: { value: "worker-1" } });
    const file = new File(["front"], "front.jpg", { type: "image/jpeg" });
    fireEvent.change(screen.getByLabelText("모바일 앞면 사진 선택"), { target: { files: [file] } });

    expect(screen.getByAltText("앞면 저장 전 미리보기")).toBeInTheDocument();
    expect(busbarApi.uploadPhoto).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("tab", { name: /뒷면/ }));
    expect(screen.getByText("등록된 사진이 없습니다.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: /앞면/ }));
    expect(screen.getByAltText("앞면 저장 전 미리보기")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "앞면 사진 크게 보기" }));
    const zoom = screen.getByRole("dialog", { name: "앞면 사진 크게 보기" });
    expect(screen.getByRole("button", { name: "확대 사진 닫기" })).toHaveFocus();
    fireEvent.keyDown(zoom, { key: "Escape" });
    expect(screen.queryByRole("dialog", { name: "앞면 사진 크게 보기" })).not.toBeInTheDocument();
    expect(screen.getByAltText("앞면 저장 전 미리보기")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("button", { name: "앞면 사진 크게 보기" })).toHaveFocus());

    fireEvent.click(screen.getByRole("button", { name: "앞면 사진 저장" }));
    await waitFor(() => expect(busbarApi.uploadPhoto).toHaveBeenCalledWith("user", "panel-1", "front", file, "worker-1"));
    expect(run).toHaveBeenCalledTimes(1);
  });

  it("keeps completed production photos read-only", async () => {
    const complete = { ...draftProduct, status: "Complete" as const, hasFront: true, hasBack: true, workerId: "worker-1", workerName: "작업자 A" };
    render(<BusbarMobilePhotoWorkspace user="user" product={complete} workers={workers} canWrite={false} busy={false} run={vi.fn()} />);

    expect(await screen.findByAltText("앞면 등록 사진")).toBeInTheDocument();
    expect(screen.getByText("완료된 생산 사진은 변경할 수 없습니다.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "앞면 사진 저장" })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("실제 제조 작업자")).not.toBeInTheDocument();
  });

  it("uses the authenticated server preview for HEIC and blocks save when preview fails", async () => {
    const run = vi.fn(async (action: () => Promise<unknown>) => { await action(); return true; });
    render(<BusbarMobilePhotoWorkspace user="user" product={draftProduct} workers={workers} canWrite busy={false} run={run} />);
    fireEvent.change(screen.getByLabelText("실제 제조 작업자"), { target: { value: "worker-1" } });
    const file = new File(["heic"], "front.heic", { type: "image/heic" });
    fireEvent.change(screen.getByLabelText("모바일 앞면 사진 선택"), { target: { files: [file] } });
    expect(await screen.findByAltText("앞면 저장 전 미리보기")).toBeInTheDocument();
    expect(busbarApi.previewPhoto).toHaveBeenCalledWith("user", "panel-1", file);

    vi.mocked(busbarApi.previewPhoto).mockRejectedValueOnce(new Error("HEIC 미리보기 실패"));
    fireEvent.change(screen.getByLabelText("모바일 앞면 사진 선택"), { target: { files: [file] } });
    expect(await screen.findByRole("alert")).toHaveTextContent("HEIC 미리보기 실패");
    expect(screen.getByRole("button", { name: "앞면 사진 저장" })).toBeDisabled();
    expect(busbarApi.uploadPhoto).not.toHaveBeenCalled();
  });

  it("drops an unfinished HEIC draft when a routed product changes", async () => {
    let finishPreview!: (blob: Blob) => void;
    vi.mocked(busbarApi.previewPhoto).mockImplementationOnce(() => new Promise((resolve) => { finishPreview = resolve; }));
    const run = vi.fn(async (action: () => Promise<unknown>) => { await action(); return true; });
    const view = render(<BusbarMobilePhotoWorkspace key="panel-1" user="user" product={draftProduct} workers={workers} canWrite busy={false} run={run} />);
    fireEvent.change(screen.getByLabelText("실제 제조 작업자"), { target: { value: "worker-1" } });
    const file = new File(["heic"], "front.heic", { type: "image/heic" });
    fireEvent.change(screen.getByLabelText("모바일 앞면 사진 선택"), { target: { files: [file] } });
    expect(screen.getByText("사진 미리보기 준비 중…")).toBeInTheDocument();

    const nextProduct = { ...draftProduct, id: "panel-2", planSequence: 2 };
    view.rerender(<BusbarMobilePhotoWorkspace key="panel-2" user="user" product={nextProduct} workers={workers} canWrite busy={false} run={run} />);
    finishPreview(new Blob(["preview"], { type: "image/jpeg" }));

    await waitFor(() => expect(screen.getByText("등록된 사진이 없습니다.")).toBeInTheDocument());
    expect(screen.queryByAltText("앞면 저장 전 미리보기")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "앞면 사진 저장" })).toBeDisabled();
    expect(busbarApi.uploadPhoto).not.toHaveBeenCalled();
  });
});
