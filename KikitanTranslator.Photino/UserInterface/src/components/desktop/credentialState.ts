export type CredentialDraft = {
    value: string;
    requestId: number | null;
    operation: "save" | "remove";
    message: string;
    severity: "success" | "warning";
};
export type CredentialDrafts = Record<string, CredentialDraft>;
export const emptyCredential: CredentialDraft = {value: "", requestId: null, operation: "save", message: "", severity: "success"};
export type CredentialAction =
    | {type: "edit"; field: string; value: string}
    | {type: "saving"; field: string; requestId: number; operation: "save" | "remove"}
    | {type: "saved" | "timeout"; field: string; requestId: number}
    | {type: "failed"};

// Drafts live only in the open settings session, keyed by credential rather than page.
export function credentialReducer(state: CredentialDrafts, action: CredentialAction): CredentialDrafts {
    if (action.type === "failed") {
        return Object.fromEntries(Object.entries(state).map(([field, draft]) => [field, draft.requestId === null ? draft : {
            ...draft, requestId: null, message: "Save was not confirmed. Check the message above and try again.", severity: "warning"
        }]));
    }
    const draft = state[action.field] || emptyCredential;
    let next: CredentialDraft;
    switch (action.type) {
        case "edit":
            if (draft.requestId !== null) return state;
            next = {...draft, value: action.value, message: ""};
            break;
        case "saving":
            next = {...draft, requestId: action.requestId, operation: action.operation, message: ""};
            break;
        case "saved":
            if (draft.requestId !== action.requestId) return state;
            next = {...draft, value: draft.operation === "save" ? "" : draft.value, requestId: null,
                message: draft.operation === "save" ? "API key saved securely." : "Saved key removed.", severity: "success"};
            break;
        case "timeout":
            if (draft.requestId !== action.requestId) return state;
            next = {...draft, requestId: null, message: "Save was not confirmed. Your draft is still here; try again.", severity: "warning"};
            break;
    }
    return {...state, [action.field]: next};
}
