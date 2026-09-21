import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { BusbarAttachmentDialog, BusbarLabelEntryPrompt } from "../src/BusbarLabelTracking";
import { busbarApi, type BusbarProduct } from "../src/interiorBusbar";
vi.mock("../src/InteriorBusbarPage", () => ({ BusbarDialog: ({ children, label }: {children: React.ReactNode; label: string}) => <div role="dialog" aria-label={label}>{children}</div> }));
vi.mock("../src/interiorBusbar", async original => ({ ...await original<typeof import("../src/interiorBusbar")>(), busbarApi: { access: vi.fn(), pendingLabels: vi.fn(), resolveLabel: vi.fn(), write: vi.fn() } }));
const panel = (id: string): BusbarProduct => ({id, number:`IB-0000000${id}`, productFamilyId:"family", planDate:"2026-09-21", workerId:null,workerName:null,status:"Draft",hasFront:false,hasBack:false,revision:1,labelState:"Printed"});
beforeEach(() => { vi.resetAllMocks(); vi.mocked(busbarApi.access).mockResolvedValue({production:true} as never); });
afterEach(cleanup);
it("saves only selected panels and leaves remaining visible", async () => {
 vi.mocked(busbarApi.write).mockResolvedValue({id:"saved"}); const changed=vi.fn();
 render(<BusbarAttachmentDialog user="a" products={[panel("1"),panel("2")]} onClose={vi.fn()} onChanged={changed}/>);
 fireEvent.click(screen.getAllByRole("checkbox")[1]); fireEvent.click(screen.getByRole("button",{name:"선택 1개 부착 완료"}));
 await waitFor(()=>expect(changed).toHaveBeenCalledOnce()); expect(busbarApi.write).toHaveBeenCalledWith("a","/labels/attached",expect.objectContaining({productIds:["1"]})); expect(screen.queryByText("IB-00000001")).toBeNull(); expect(screen.getByText("IB-00000002")).toBeTruthy();
});
it("reuses idempotency request after failed response", async () => {
 vi.mocked(busbarApi.write).mockRejectedValueOnce(new Error("연결 실패")).mockResolvedValueOnce({id:"saved"});
 render(<BusbarAttachmentDialog user="a" products={[panel("1")]} onClose={vi.fn()} onChanged={vi.fn()}/>);
 fireEvent.click(screen.getByRole("button",{name:"선택 1개 부착 완료"})); await screen.findByRole("alert"); fireEvent.click(screen.getByRole("button",{name:"선택 1개 부착 완료"}));
 await waitFor(()=>expect(busbarApi.write).toHaveBeenCalledTimes(2)); expect(vi.mocked(busbarApi.write).mock.calls[0][2]).toEqual(vi.mocked(busbarApi.write).mock.calls[1][2]);
});
it("number lookup selects result without mutating confirmation", async () => {
 vi.mocked(busbarApi.resolveLabel).mockResolvedValue(panel("1")); render(<BusbarAttachmentDialog user="a" products={[]} onClose={vi.fn()} onChanged={vi.fn()}/>);
 fireEvent.change(screen.getByRole("textbox"),{target:{value:"0001"}}); fireEvent.click(screen.getByRole("button",{name:"찾기"})); await screen.findByText("IB-00000001"); expect(busbarApi.resolveLabel).toHaveBeenCalledWith("a","0001"); expect(busbarApi.write).not.toHaveBeenCalled();
});
it("entry lookup excludes unrelated panels", async () => {
 vi.mocked(busbarApi.resolveLabel).mockResolvedValue(panel("2")); render(<BusbarAttachmentDialog user="a" products={[panel("1")]} entry onClose={vi.fn()} onChanged={vi.fn()}/>);
 fireEvent.change(screen.getByRole("textbox"),{target:{value:"2"}}); fireEvent.click(screen.getByRole("button",{name:"찾기"})); await screen.findByRole("alert"); expect(screen.queryByText("IB-00000002")).toBeNull();
});
it("entry deferred prompt stays closed during same app session", async () => {
 vi.mocked(busbarApi.pendingLabels).mockResolvedValue([panel("1")]); const view=render(<BusbarLabelEntryPrompt user="a" scope="campus:a" mobile/>);
 await screen.findByRole("dialog"); fireEvent.click(screen.getByRole("button",{name:"나중에 확인"})); view.rerender(<BusbarLabelEntryPrompt user="a" scope="campus:a" mobile/>); expect(screen.queryByRole("dialog")).toBeNull(); expect(busbarApi.pendingLabels).toHaveBeenCalledTimes(1);
});
it("production denial prevents pending query", async () => {
 vi.mocked(busbarApi.access).mockResolvedValue({production:false} as never); render(<BusbarLabelEntryPrompt user="a" scope="a" mobile/>); await waitFor(()=>expect(busbarApi.access).toHaveBeenCalledOnce()); expect(busbarApi.pendingLabels).not.toHaveBeenCalled();
});
it("caps a backlog at 200 selected panels", () => {
 render(<BusbarAttachmentDialog user="a" products={Array.from({length:201},(_,i)=>panel(String(i+1)))} onClose={vi.fn()} onChanged={vi.fn()}/>);
 expect(screen.getByRole("button",{name:"선택 200개 부착 완료"})).toBeTruthy(); expect(screen.getAllByRole("checkbox")[200]).toBeDisabled();
});
it("refreshes an open prompt on app return and closes when others attached everything", async () => {
 let hidden=false; vi.spyOn(document,"hidden","get").mockImplementation(()=>hidden);
 const now=vi.spyOn(Date,"now").mockReturnValue(1_000);
 vi.mocked(busbarApi.pendingLabels).mockResolvedValueOnce([panel("1")]).mockResolvedValueOnce([]);
 render(<BusbarLabelEntryPrompt user="a" scope="a" mobile/>); await screen.findByRole("dialog");
 hidden=true; fireEvent(document,new Event("visibilitychange")); now.mockReturnValue(62_000); hidden=false; fireEvent(document,new Event("visibilitychange"));
 await waitFor(()=>expect(screen.queryByRole("dialog")).toBeNull()); expect(busbarApi.pendingLabels).toHaveBeenCalledTimes(2); vi.restoreAllMocks();
});
it("lookup at the selection limit explains the limit without claiming selection", async () => {
 vi.mocked(busbarApi.resolveLabel).mockResolvedValue(panel("201")); render(<BusbarAttachmentDialog user="a" products={Array.from({length:200},(_,i)=>panel(String(i+1)))} onClose={vi.fn()} onChanged={vi.fn()}/>);
 fireEvent.change(screen.getByRole("textbox"),{target:{value:"201"}}); fireEvent.click(screen.getByRole("button",{name:"찾기"})); await screen.findByRole("alert"); expect(screen.queryByRole("status")).toBeNull(); expect(screen.getAllByRole("checkbox")).toHaveLength(200);
});
