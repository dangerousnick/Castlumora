import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { authenticatedMediaUrl } from '../player/profile';
export default function PhotoViewer() {
  const { id = '' } = useParams();
  const { api, session } = useApp();
  const navigate = useNavigate();
  const [zoom, setZoom] = useState(1);
  const [rotation, setRotation] = useState(0);
  const [slideshow, setSlideshow] = useState(false);
  const result = useLoad(async () => {
    const item = await api.item(id);
    if (item.Type !== 'Photo') throw new Error('This item is not a photo.');
    const neighbors = await api.items({
      IncludeItemTypes: 'Photo',
      ParentId: item.ParentId,
      Recursive: false,
      AdjacentTo: id,
      SortBy: 'SortName',
      Limit: 3,
    });
    const index = neighbors.Items.findIndex((value) => value.Id === id);
    return {
      item,
      previous: index > 0 ? neighbors.Items[index - 1]?.Id : undefined,
      next: index >= 0 ? neighbors.Items[index + 1]?.Id : undefined,
    };
  }, [api, id]);
  useEffect(() => {
    if (!slideshow || !result.value?.next) return;
    const timer = setTimeout(
      () => navigate('/photo/' + result.value!.next, { replace: true }),
      5000,
    );
    return () => clearTimeout(timer);
  }, [slideshow, result.value, navigate]);
  const item = result.value?.item;
  return (
    <main className="page photo-viewer">
      <Link to={item?.ParentId ? '/item/' + item.ParentId : '/shelf/photos'}>← Back to photos</Link>
      <Feedback busy={result.busy} error={result.error} />
      {item && (
        <>
          <h1>{item.Name}</h1>
          <div className="reader-toolbar">
            <button
              disabled={!result.value?.previous}
              onClick={() => {
                setZoom(1);
                setRotation(0);
                navigate('/photo/' + result.value!.previous);
              }}
            >
              Previous photo
            </button>
            <button
              disabled={!result.value?.next}
              onClick={() => {
                setZoom(1);
                setRotation(0);
                navigate('/photo/' + result.value!.next);
              }}
            >
              Next photo
            </button>
            <label>
              Photo zoom
              <select value={zoom} onChange={(e) => setZoom(Number(e.target.value))}>
                <option value="1">Fit</option>
                <option value="1.5">150%</option>
                <option value="2">200%</option>
              </select>
            </label>
            <button onClick={() => setRotation((rotation + 90) % 360)}>Rotate photo</button>
            <button aria-pressed={slideshow} onClick={() => setSlideshow(!slideshow)}>
              {slideshow ? 'Stop slideshow' : 'Start slideshow'}
            </button>
            {session.user.Policy?.EnableContentDownloading && item.CanDownload !== false && (
              <a
                href={authenticatedMediaUrl(api, api.url(`Items/${id}/Download`).href)}
                rel="noopener noreferrer"
              >
                Download photo
              </a>
            )}
          </div>
          <div className="photo-canvas">
            <img
              alt={item.Name}
              src={authenticatedMediaUrl(
                api,
                api.image(item, 'Primary', 2400) ?? api.url(`Items/${id}/File`).href,
              )}
              style={{ transform: `scale(${zoom}) rotate(${rotation}deg)` }}
            />
          </div>
        </>
      )}
    </main>
  );
}
