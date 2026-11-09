import { lazy, Suspense, useEffect, useMemo, useState } from "react";
import type { CSSProperties } from "react";
import { HashRouter, Link, NavLink, Route, Routes, useNavigate } from "react-router-dom";
import { Dices, LogOut, Settings, Radio } from "lucide-react";
import { Api, ApiError } from "./api/client";
import type { Preferences, Session } from "./api/types";
import { defaultLayout, deviceClass } from "./config/schema";
import type { Layout } from "./config/schema";
import { AppContext, errorText } from "./state";
import Login, { LinkBrand } from "./pages/Login";
import Home from "./pages/Home";
import Browse from "./pages/Browse";
import Detail from "./pages/Detail";
import Live from "./pages/Live";
import Admin from "./pages/Admin";
import { Feedback } from "./components/Media";
const Player = lazy(() => import("./player/Player"));
function readSession(): Session | null {
    try {
        const value = JSON.parse(sessionStorage.getItem("castlumora-session") ?? "null");
        return value?.server && value?.token && value?.user?.Id ? value : null;
    } catch {
        return null;
    }
}
function Shell({ api, session, logout }: { api: Api; session: Session; logout: () => Promise<void> }) {
    const [layout, setLayout] = useState<Layout>(structuredClone(defaultLayout));
    const [preferences, setPreferences] = useState<Preferences>({ watchlist: [], hiddenResume: [] });
    const [notice, setNotice] = useState("");
    const [error, setError] = useState("");
    const [ready, setReady] = useState(false);
    const [width, setWidth] = useState(window.innerWidth);
    const navigate = useNavigate();
    async function refreshLayout() {
        const value = await api.layout();
        setLayout(value.config);
        setNotice(value.fallback ? "This server does not yet support presentation configuration. Using default layout." : "");
    }
    useEffect(() => {
        let alive = true;
        void Promise.all([api.me(), api.layout(), api.preferences()])
            .then(([user, result, prefs]) => {
                if (!alive) return;
                session.user = user;
                setLayout(result.config);
                setPreferences(prefs);
                setNotice(result.fallback ? "Presentation configuration is unavailable on this server." : "");
                setReady(true);
            })
            .catch((e) => {
                if (e instanceof ApiError && e.status === 401) void logout();
                else if (alive) setError(errorText(e));
            });
        return () => {
            alive = false;
        };
    }, [api, session, logout]);
    useEffect(() => {
        const resize = () => setWidth(window.innerWidth);
        window.addEventListener("resize", resize);
        return () => window.removeEventListener("resize", resize);
    }, []);
    async function surprise() {
        try {
            const items = await api.items({ SortBy: "Random", Limit: 1, IncludeItemTypes: "Movie" });
            if (items.Items[0]) navigate("/item/" + items.Items[0].Id);
            else setNotice("No movies are available to this profile yet.");
        } catch (e) {
            setNotice(errorText(e));
        }
    }
    const device = deviceClass(width);
    const settings = layout.devices[device];
    const style = { "--accent": layout.theme.accent, "--background": layout.theme.background, "--poster-width": `${settings?.posterWidth ?? 180}px`, "--safe-zone": `${settings?.safeZone ?? 0}vw` } as CSSProperties;
    return (
        <AppContext.Provider value={{ api, session, layout, preferences, setPreferences, refreshLayout, logout, notice }}>
            <div className="app" style={style}>
                <a className="skip-link" href="#content">
                    Skip to content
                </a>
                <header className="topbar">
                    <Link to="/" aria-label="Castlumora home">
                        <LinkBrand />
                    </Link>
                    <nav aria-label="Primary">
                        {layout.navigation.map((n) => (
                            <NavLink key={n.id} to={{ home: "/", browse: "/browse", list: "/list", search: "/search" }[n.id]} end>
                                {n.label}
                            </NavLink>
                        ))}
                    </nav>
                    <div className="header-actions">
                        <button className="icon-button" onClick={() => void surprise()} title="Surprise Me" aria-label="Surprise Me">
                            <Dices size={20} />
                        </button>
                        {session.user.Policy?.IsAdministrator && (
                            <Link className="icon-button" to="/admin" aria-label="Administration">
                                <Settings size={20} />
                            </Link>
                        )}
                        <details className="profile-menu">
                            <summary>
                                <span className="avatar">{session.user.Name.slice(0, 1)}</span>
                                <span>{session.user.Name}</span>
                            </summary>
                            <div className="menu">
                                <Link to="/live">
                                    <Radio size={15} />
                                    Live TV
                                </Link>
                                <a href={api.legacy()} target="_blank" rel="noopener noreferrer">
                                    Existing client ↗
                                </a>
                                <button onClick={() => void logout()}>
                                    <LogOut size={15} />
                                    Sign out / switch profile
                                </button>
                            </div>
                        </details>
                    </div>
                </header>
                {notice && (
                    <p role="status" className="notice global-notice">
                        {notice}
                    </p>
                )}
                <div id="content" tabIndex={-1}>
                    <Feedback busy={!ready && !error} error={error} />
                    {ready && (
                        <Suspense fallback={<Feedback busy />}>
                            <Routes>
                                <Route path="/" element={<Home />} />
                                <Route path="/browse" element={<Browse />} />
                                <Route path="/search" element={<Browse />} />
                                <Route path="/list" element={<Browse />} />
                                <Route path="/item/:id" element={<Detail />} />
                                <Route path="/play/:id" element={<Player key={window.location.hash} />} />
                                <Route path="/live" element={<Live />} />
                                <Route path="/admin" element={<Admin />} />
                                <Route
                                    path="*"
                                    element={
                                        <main className="page">
                                            <h1>Page not found</h1>
                                            <Link to="/">Return home</Link>
                                        </main>
                                    }
                                />
                            </Routes>
                        </Suspense>
                    )}
                </div>
                <footer>
                    <LinkBrand />
                    <span>Your media. Your world.</span>
                    <a href={api.legacy()} target="_blank" rel="noopener noreferrer">
                        Existing client ↗
                    </a>
                    <small>Castlumora · Free & open source · Built on Jellyfin · GPL</small>
                </footer>
            </div>
        </AppContext.Provider>
    );
}
export default function App() {
    const [session, setSession] = useState(readSession);
    const api = useMemo(() => (session ? new Api(session.server, session) : null), [session]);
    const logout = useMemo(
        () => async () => {
            try {
                await api?.logout();
            } finally {
                sessionStorage.removeItem("castlumora-session");
                setSession(null);
            }
        },
        [api],
    );
    return (
        <HashRouter>
            {session && api ? (
                <Shell api={api} session={session} logout={logout} />
            ) : (
                <Login
                    onLogin={(s) => {
                        sessionStorage.setItem("castlumora-session", JSON.stringify(s));
                        setSession(s);
                    }}
                />
            )}
        </HashRouter>
    );
}
