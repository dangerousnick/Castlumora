import { createContext, useContext, useEffect, useState } from "react";
import type { DependencyList } from "react";
import type { Api } from "./api/client";
import type { Preferences, Session } from "./api/types";
import type { Layout } from "./config/schema";
export interface AppState {
    api: Api;
    session: Session;
    layout: Layout;
    preferences: Preferences;
    setPreferences: (p: Preferences) => void;
    refreshLayout: () => Promise<void>;
    logout: () => Promise<void>;
    notice: string;
}
export const AppContext = createContext<AppState | null>(null);
export function useApp() {
    const state = useContext(AppContext);
    if (!state) throw new Error("Sign in required");
    return state;
}
export function useLoad<T>(loader: () => Promise<T>, deps: DependencyList) {
    const [value, setValue] = useState<T>();
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(true);
    useEffect(() => {
        let active = true;
        setBusy(true);
        setError("");
        setValue(undefined);
        loader()
            .then((v) => {
                if (active) setValue(v);
            })
            .catch((e) => {
                if (active) setError(e instanceof Error ? e.message : "Unable to load data");
            })
            .finally(() => {
                if (active) setBusy(false);
            });
        return () => {
            active = false;
        };
        // Callers explicitly declare the request identity, like a query key.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, deps);
    return { value, error, busy };
}
export function errorText(error: unknown) {
    return error instanceof Error ? error.message : "Something went wrong. Please try again.";
}
