import { useEffect, useRef, useState } from "react";
import { pointsDeltaText } from "@cafecr/ui";

const css = `
@keyframes cr-points-pop {
  0% { opacity: 0; transform: translateY(8px) scale(.9); }
  15% { opacity: 1; transform: translateY(0) scale(1.08); }
  25% { transform: scale(1); }
  80% { opacity: 1; }
  100% { opacity: 0; transform: translateY(-10px); }
}
@media (prefers-reduced-motion: reduce) { .cr-points-pop { animation-duration: .01s !important; } }
`;

/**
 * Counts the shown number up to the new value and pops "+N điểm" for ~2.5 s when points change while
 * the page is open (not on the first load).
 */
export function useAnimatedPoints(points: number | undefined) {
  const [shown, setShown] = useState(points ?? 0);
  const [delta, setDelta] = useState<{ text: string; key: number } | null>(null);
  const last = useRef<number | undefined>(undefined);

  useEffect(() => {
    if (points === undefined) return;
    const from = last.current;
    last.current = points;
    if (from === undefined) { setShown(points); return; }
    const text = pointsDeltaText(from, points);
    if (!text) return;
    setDelta({ text, key: Date.now() });
    const start = performance.now();
    let frame = 0;
    const step = (t: number) => {
      const k = Math.min(1, (t - start) / 900);
      setShown(Math.round(from + (points - from) * (1 - (1 - k) ** 3)));
      if (k < 1) frame = requestAnimationFrame(step);
    };
    frame = requestAnimationFrame(step);
    const hide = window.setTimeout(() => setDelta(null), 2600);
    return () => { cancelAnimationFrame(frame); window.clearTimeout(hide); setShown(points); };
  }, [points]);

  return { shown, delta };
}

export function PointsDeltaBadge({ delta }: { delta: { text: string; key: number } | null }) {
  if (!delta) return null;
  const gain = delta.text.startsWith("+");
  return (
    <>
      <style>{css}</style>
      <span key={delta.key} className="cr-points-pop" role="status"
        style={{
          display: "inline-block", marginLeft: 10, padding: "2px 10px", borderRadius: 12, fontWeight: 700, fontSize: 15,
          color: "#fff", background: gain ? "#389e0d" : "#cf1322", animation: "cr-points-pop 2.5s ease-out forwards",
        }}>
        {delta.text}
      </span>
    </>
  );
}
