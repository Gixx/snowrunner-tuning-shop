import { useEffect, useState } from "react";

function layer(size: number, density: number, opacity: number) {
  const dots: string[] = [];
  for (let i = 0; i < density; i++) {
    const x = Math.round(Math.random() * 100);
    const y = Math.round(Math.random() * 100);
    dots.push(`radial-gradient(${size}px ${size}px at ${x}% ${y}%, rgba(255,255,255,${opacity}), transparent 60%)`);
  }
  return dots.join(",");
}

/** Pure-CSS snowfall: three parallax layers, hydration-safe. */
export function Snowfall() {
  const [layers, setLayers] = useState<{ bg: string; dur: number; size: number }[]>([]);

  useEffect(() => {
    setLayers([
      { bg: layer(2, 60, 0.55), dur: 26, size: 900 },
      { bg: layer(3, 40, 0.4), dur: 18, size: 700 },
      { bg: layer(4, 22, 0.3), dur: 12, size: 500 },
    ]);
  }, []);

  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden">
      {layers.map((l, i) => (
        <div
          key={i}
          className="snow-layer"
          style={{
            backgroundImage: l.bg,
            backgroundSize: `${l.size}px ${l.size}px`,
            animationDuration: `${l.dur}s`,
            animationDelay: `-${i * 4}s`,
          }}
        />
      ))}
    </div>
  );
}
