export type PanelKittingQueueResponse = {
  projects: PanelKittingProject[];
};

export type PanelKittingProject = {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  activeItemCount: number;
  completedItemCount: number;
  ready: boolean;
  pendingPanelCount: number;
  completedPanelCount: number;
  panels: PanelKittingPanel[];
};

export type PanelKittingPanel = {
  panelId: string;
  displayCode: string;
  panelName: string | null;
  panelInfoCompleted: boolean;
  kittingCompleted: boolean;
  completedAtUtc: string | null;
  completedByDisplayName: string | null;
  selectable: boolean;
};

export type CompletePanelKittingRequest = {
  operationId: string;
  projectId: string;
  panelIds: string[];
};

export type PanelKittingCompletionResponse = {
  operationId: string;
  completedPanelCount: number;
  generatedWorkItemCount: number;
  projectKittingCompleted: boolean;
  replayed: boolean;
};
