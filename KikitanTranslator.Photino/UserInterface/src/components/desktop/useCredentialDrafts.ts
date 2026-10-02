import {useEffect, useReducer, useRef} from "react";
import {registerConfigSavedCallback, setConfig} from "../../util/photino";
import {credentialReducer, emptyCredential} from "./credentialState";

export function useCredentialDrafts(failures: number) {
    const [drafts, dispatch] = useReducer(credentialReducer, {});
    const timers = useRef(new Map<string, {requestId: number; timer: number}>());
    function clearTimer(field: string) {
        const entry = timers.current.get(field);
        if (entry) window.clearTimeout(entry.timer);
        timers.current.delete(field);
    }
    useEffect(() => {
        const unsubscribe = registerConfigSavedCallback((field, requestId) => {
            if (timers.current.get(field)?.requestId === requestId) clearTimer(field);
            dispatch({type: "saved", field, requestId});
        });
        return () => {
            unsubscribe();
            timers.current.forEach(entry => window.clearTimeout(entry.timer));
            timers.current.clear();
        };
    }, []);
    useEffect(() => {
        timers.current.forEach(entry => window.clearTimeout(entry.timer));
        timers.current.clear();
        dispatch({type: "failed"});
    }, [failures]);
    function save(field: string, value: string) {
        if (drafts[field]?.requestId != null) return;
        const requestId = setConfig(field, value);
        dispatch({type: "saving", field, requestId, operation: value ? "save" : "remove"});
        clearTimer(field);
        const timer = window.setTimeout(() => {
            timers.current.delete(field);
            dispatch({type: "timeout", field, requestId});
        }, 10000);
        timers.current.set(field, {requestId, timer});
    }
    return {
        get: (field: string) => drafts[field] || emptyCredential,
        edit: (field: string, value: string) => dispatch({type: "edit", field, value}),
        save
    };
}
