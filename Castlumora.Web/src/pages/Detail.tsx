import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Play, Plus, Check, ArrowLeft } from "lucide-react";
import { sameId } from "../api/client";
import { errorText, useApp, useLoad } from "../state";
import { Feedback } from "../components/Media";
function Episodes({ seriesId, seasonId }: { seriesId: string; seasonId: string }) {
    const { api } = useApp();
    const result = useLoad(() => api.episodes(seriesId, seasonId), [api, seriesId, seasonId]);
    return (
        <>
            <Feedback busy={result.busy} error={result.error} />
            <div className="episodes">
                {result.value?.Items.map((i) => (
                    <Link to={"/play/" + i.Id} key={i.Id} className="episode">
                        <div>
                            {api.image(i) && <img src={api.image(i)} alt="" loading="lazy" />}
                            <Play />
                        </div>
                        <span>
                            <small>EPISODE {i.IndexNumber}</small>
                            <h3>{i.Name}</h3>
                            <p>{i.Overview}</p>
                        </span>
                        <small>{i.RunTimeTicks ? Math.round(i.RunTimeTicks / 600000000) + " min" : ""}</small>
                    </Link>
                ))}
            </div>
        </>
    );
}
export default function Detail() {
    const { id = "" } = useParams();
    const { api, preferences, setPreferences } = useApp();
    const [error, setError] = useState("");
    const [season, setSeason] = useState("");
    const result = useLoad(async () => {
        const item = await api.item(id);
        const seasons = item.Type === "Series" ? (await api.seasons(id)).Items : [];
        return { item, seasons };
    }, [api, id]);
    const item = result.value?.item;
    const listed = preferences.watchlist.some((i) => sameId(i, id));
    async function toggle() {
        try {
            setPreferences(await api.preference("watchlist", id, !listed));
        } catch (e) {
            setError(errorText(e));
        }
    }
    return (
        <>
            <Feedback busy={result.busy} error={result.error} />
            {item && (
                <>
                    <section className="detail-hero" style={{ backgroundImage: `linear-gradient(90deg,#0d0b14,rgba(13,11,20,.4)),linear-gradient(0deg,var(--background),transparent),url("${api.image(item, "Backdrop", 1920) ?? ""}")` }}>
                        <Link to="/browse" className="back-link">
                            <ArrowLeft size={18} />
                            Back to browse
                        </Link>
                        <div className="detail-copy">
                            <span className="eyebrow">{item.Type === "Series" ? "GET LOST IN A GREAT SERIES" : "TONIGHT’S FEATURE"}</span>
                            <h1>{item.Name}</h1>
                            <div className="metadata">
                                <span>{item.ProductionYear}</span>
                                <span>{item.OfficialRating}</span>
                                {item.RunTimeTicks && <span>{Math.round(item.RunTimeTicks / 600000000)} min</span>}
                                <span>{item.Genres?.join(" · ")}</span>
                            </div>
                            <p>{item.Overview}</p>
                            <div className="actions">
                                {item.Type !== "Series" && (
                                    <Link className="button primary" to={"/play/" + id}>
                                        <Play size={18} />
                                        {item.UserData?.PlaybackPositionTicks ? "Resume" : "Watch now"}
                                    </Link>
                                )}
                                <button onClick={() => void toggle()}>
                                    {listed ? <Check size={18} /> : <Plus size={18} />} {listed ? "In My List" : "Add to My List"}
                                </button>
                            </div>
                            {error && (
                                <p role="alert" className="error">
                                    {error}
                                </p>
                            )}
                        </div>
                    </section>
                    {result.value!.seasons.length > 0 && (
                        <section className="page season-section">
                            <div className="section-heading">
                                <h2>One more episode?</h2>
                                <select aria-label="Season" value={season || result.value!.seasons[0].Id} onChange={(e) => setSeason(e.target.value)}>
                                    {result.value!.seasons.map((s) => (
                                        <option key={s.Id} value={s.Id}>
                                            {s.Name}
                                        </option>
                                    ))}
                                </select>
                            </div>
                            <Episodes seriesId={id} seasonId={season || result.value!.seasons[0].Id} />
                        </section>
                    )}
                </>
            )}
        </>
    );
}
