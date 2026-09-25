import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import {
  Download,
  Github,
  Gauge,
  Fuel,
  Cog,
  Lock,
  ShieldCheck,
  Layers,
  ArrowRight,
  Wrench,
  CircleAlert,
} from "lucide-react";

import { Snowfall } from "@/components/Snowfall";
import { Reveal } from "@/components/Reveal";
import { Lightbox, type GalleryShot } from "@/components/Lightbox";
import shotHome from "@/assets/shot-home.png";
import shotGeneral from "@/assets/shot-general.png";
import shotParts from "@/assets/shot-parts.png";
import shotVehicles from "@/assets/shot-vehicles.png";
import shotVehicle from "@/assets/shot-vehicle.png";
import shotTrailers from "@/assets/shot-trailers.png";
import shotTrailer from "@/assets/shot-trailer.png";
import shotSettings from "@/assets/shot-settings.png";
import shotHomeLinux from "@/assets/shot-home-linux.png";
import shotGeneralLinux from "@/assets/shot-general-linux.png";
import shotPartsLinux from "@/assets/shot-parts-linux.png";
import shotVehiclesLinux from "@/assets/shot-vehicles-linux.png";
import shotVehicleLinux from "@/assets/shot-vehicle-linux.png";
import shotTrailersLinux from "@/assets/shot-trailers-linux.png";
import shotTrailerLinux from "@/assets/shot-trailer-linux.png";
import shotSettingsLinux from "@/assets/shot-settings-linux.png";
import {
  SITE_DESCRIPTION,
  SITE_TITLE,
  APP_VERSION_STABLE,
  APP_VERSION_BETA,
  RELEASE_LATEST_URL,
  RELEASES_URL,
  REPO_URL,
} from "@/lib/site";

type Platform = "windows" | "linux";

const badges = [
  {
    href: RELEASE_LATEST_URL,
    src: "https://img.shields.io/github/v/release/Gixx/snowrunner-tuning-shop?style=flat-square&label=version&color=38bdf8",
    alt: "Latest release version",
  },
  {
    href: "https://learn.microsoft.com/dotnet/csharp/",
    src: "https://img.shields.io/badge/language-C%23-239120?style=flat-square&logo=csharp&logoColor=white",
    alt: "C#",
  },
  {
    href: "https://learn.microsoft.com/dotnet/desktop/wpf/",
    src: "https://img.shields.io/badge/WPF-.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white",
    alt: "WPF on .NET",
  },
  {
    href: "https://avaloniaui.net/",
    src: "https://img.shields.io/badge/Avalonia-.NET-8B44FF?style=flat-square&logo=avalonia&logoColor=white",
    alt: "Avalonia on .NET",
  },
  {
    href: RELEASE_LATEST_URL,
    src: "https://img.shields.io/badge/platform-Windows%2FLinux-0078D4?style=flat-square&logo=windows&logoColor=white",
    alt: "Windows/Linux",
  },
  {
    href: RELEASE_LATEST_URL,
    src: "https://img.shields.io/github/downloads/Gixx/snowrunner-tuning-shop/total.svg?style=flat-square&label=downloads&color=e11d48",
    alt: "GitHub release downloads",
  },
  {
    href: `${REPO_URL}/blob/main/LICENSE`,
    src: "https://img.shields.io/github/license/Gixx/snowrunner-tuning-shop?style=flat-square",
    alt: "MIT license",
  },
];

function BadgeRow({ className = "" }: { className?: string }) {
  return (
    <div className={`flex flex-wrap items-center gap-2 ${className}`}>
      {badges.map((badge) => (
        <a
          key={badge.alt}
          href={badge.href}
          target="_blank"
          rel="noreferrer"
          className="inline-flex opacity-90 transition-opacity hover:opacity-100"
        >
          <img src={badge.src} alt={badge.alt} height={20} />
        </a>
      ))}
    </div>
  );
}

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: SITE_TITLE },
      { name: "description", content: SITE_DESCRIPTION },
    ],
  }),
  component: Index,
});

const whatsNew = [
  {
    icon: CircleAlert,
    title: "Trucks stay visible",
    body: "Adding rear counter-steer no longer writes a broken `/ SteeringAngle` tag. Trucks like the White Western Star stay in garage and store; re-save repairs already-broken XML.",
  },
  {
    icon: ShieldCheck,
    title: "Rewrite safety in CI",
    body: "A fixture matrix now round-trips multipliers and edge values through every major rewrite path and fails the build if a self-close slash lands before an attribute.",
  },
  {
    icon: Wrench,
    title: "Local pak smoke",
    body: "Optional console runner walks your real baseline pak with a progress bar — structural checks only, not run by CI.",
  },
];

const features = [
  {
    icon: Cog,
    title: "Every part, one table",
    body: "Engines, gearboxes, suspensions, winches and tires listed with price, torque, damage and responsiveness. Edit a single row, or scale the whole class with global multipliers.",
  },
  {
    icon: Fuel,
    title: "Fuel that makes sense",
    body: "Dial consumption up for a punishing haul or down for a relaxed run. Tank capacity per truck is one field away.",
  },
  {
    icon: Gauge,
    title: "Steering you can feel",
    body: "Per-axle angles plus steer speed, back-steer speed, and responsiveness — turn a barge into something that actually corners.",
  },
  {
    icon: Lock,
    title: "AWD & diff lock, unforgotten",
    body: "Enable Always AWD or permanent diff lock on the trucks the game quietly left out. No upgrade hunting required.",
  },
  {
    icon: ShieldCheck,
    title: "Baseline safety net",
    body: "Your original initial.pak is backed up on first run. Restore a category, or everything at once. After a game update, refresh the baseline and reapply your saved tunings.",
  },
  {
    icon: Layers,
    title: "Base game + 49 DLC packs",
    body: "Reads all 11 700+ XML entries, 118 vehicles and every DLC package straight out of the archive.",
  },
];

const stats: [string, string][] = [
  ["118", "vehicles"],
  ["125", "engines"],
  ["214", "suspensions"],
  ["49", "DLC packs"],
];

const windowsShots: GalleryShot[] = [
  {
    src: shotTrailer,
    alt: "Trailer tuning panel for the fishing-boat semi with store price, store availability and unlock rank",
    label: "Trailer tuning",
  },
  {
    src: shotHome,
    alt: "SnowRunner Tuning Shop home screen showing baseline status and pak overview",
    label: "Home — baseline status",
  },
  {
    src: shotGeneral,
    alt: "General tuning with camera collision mode and trail rock size slider",
    label: "General tuning",
  },
  {
    src: shotParts,
    alt: "Engine list with global torque, fuel and responsiveness multipliers",
    label: "Parts — engines",
  },
  {
    src: shotVehicles,
    alt: "Grid of 118 SnowRunner vehicles filtered by class",
    label: "Vehicles — 118 trucks",
  },
  {
    src: shotTrailers,
    alt: "Trailer catalog grid filtered by hitch, with per-trailer photos",
    label: "Trailers — 67 trailers",
  },
  {
    src: shotVehicle,
    alt: "Vehicle tuning panel for the Futom 7290RA with fuel tank, steering and store settings",
    label: "Vehicle tuning",
  },
  {
    src: shotSettings,
    alt: "Settings page with theme, language, workspace restore and update check",
    label: "Settings",
  },
];

const linuxShots: GalleryShot[] = [
  {
    src: shotTrailerLinux,
    alt: "Linux Avalonia trailer tuning panel with store price, availability and unlock rank",
    label: "Trailer tuning",
  },
  {
    src: shotHomeLinux,
    alt: "Linux Avalonia home screen showing baseline status and pak overview",
    label: "Home — baseline status",
  },
  {
    src: shotGeneralLinux,
    alt: "Linux Avalonia general tuning with camera collision mode and trail rock size",
    label: "General tuning",
  },
  {
    src: shotPartsLinux,
    alt: "Linux Avalonia engine list with global multipliers",
    label: "Parts — engines",
  },
  {
    src: shotVehiclesLinux,
    alt: "Linux Avalonia vehicle catalog grid filtered by class",
    label: "Vehicles — 118 trucks",
  },
  {
    src: shotTrailersLinux,
    alt: "Linux Avalonia trailer catalog grid filtered by hitch",
    label: "Trailers — 67 trailers",
  },
  {
    src: shotVehicleLinux,
    alt: "Linux Avalonia vehicle tuning panel with fuel, steering and store settings",
    label: "Vehicle tuning",
  },
  {
    src: shotSettingsLinux,
    alt: "Linux Avalonia settings page with theme, language and update channel",
    label: "Settings",
  },
];

const windowsHero = shotVehicle;
const linuxHero = shotVehicleLinux;

function PlatformShowcase({
  platform,
  shots,
  heroSrc,
  heroAlt,
  onOpenShot,
}: {
  platform: Platform;
  shots: GalleryShot[];
  heroSrc: string;
  heroAlt: string;
  onOpenShot: (index: number) => void;
}) {
  const heroIndex = useMemo(
    () => Math.max(0, shots.findIndex((shot) => shot.src === heroSrc)),
    [shots, heroSrc],
  );

  return (
    <div key={platform}>
      <div className="mx-auto max-w-6xl px-6">
        <Reveal>
          <button
            type="button"
            onClick={() => onOpenShot(heroIndex)}
            className="block w-full overflow-hidden rounded-md border border-border bg-card/70 p-0 text-left shadow-[0_40px_80px_-30px_rgba(0,0,0,0.9)] backdrop-blur transition-transform hover:scale-[1.01]"
            aria-label={`View ${platform} vehicle tuning screenshot full size`}
          >
            <img
              src={heroSrc}
              alt={heroAlt}
              width={1782}
              height={1221}
              className="h-auto w-full cursor-zoom-in"
            />
          </button>
        </Reveal>
      </div>

      <section className="mt-16 border-y border-border bg-card/40">
        <div className="mx-auto grid max-w-6xl grid-cols-2 gap-px px-6 md:grid-cols-4">
          {stats.map(([n, l], i) => (
            <Reveal key={`${platform}-${l}`} delay={i * 90}>
              <div className="py-10 text-center">
                <div className="font-display text-5xl text-primary">{n}</div>
                <div className="mt-1 text-xs uppercase tracking-[0.24em] text-muted-foreground">{l}</div>
              </div>
            </Reveal>
          ))}
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-6 py-24">
        <Reveal>
          <p className="mb-3 text-xs font-semibold uppercase tracking-[0.28em] text-primary">
            Version {APP_VERSION_STABLE}
          </p>
          <h2 className="font-display text-5xl tracking-tight md:text-6xl">
            WHAT&apos;S NEW IN VERSION {APP_VERSION_STABLE}
          </h2>
          <p className="mt-4 max-w-2xl text-muted-foreground">
            Safer axle writes so trucks stay in the garage — plus stricter rewrite checks before a bad slash can ship.
          </p>
        </Reveal>
        <div className="mt-14 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {whatsNew.map((item, i) => (
            <Reveal key={item.title} delay={(i % 3) * 100}>
              <article className="group h-full rounded-md border border-border bg-card/60 p-7 transition-colors duration-300 hover:border-primary/60 hover:bg-card">
                <item.icon className="size-7 text-ice transition-colors group-hover:text-primary" aria-hidden />
                <h3 className="mt-5 font-display text-2xl tracking-wide">{item.title}</h3>
                <p className="mt-3 text-sm leading-relaxed text-muted-foreground">{item.body}</p>
              </article>
            </Reveal>
          ))}
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-6 pb-24">
        <Reveal>
          <h2 className="font-display text-5xl tracking-tight md:text-6xl">WHAT YOU CAN TUNE</h2>
          <p className="mt-4 max-w-2xl text-muted-foreground">
            No text editors, no XML diffing, no broken saves. Point it at your install and start turning knobs.
          </p>
        </Reveal>
        <div className="mt-14 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {features.map((f, i) => (
            <Reveal key={`${platform}-${f.title}`} delay={(i % 3) * 100}>
              <article className="group h-full rounded-md border border-border bg-card/60 p-7 transition-colors duration-300 hover:border-primary/60 hover:bg-card">
                <f.icon className="size-7 text-ice transition-colors group-hover:text-primary" aria-hidden />
                <h3 className="mt-5 font-display text-2xl tracking-wide">{f.title}</h3>
                <p className="mt-3 text-sm leading-relaxed text-muted-foreground">{f.body}</p>
              </article>
            </Reveal>
          ))}
        </div>
      </section>

      <section className="relative overflow-hidden border-y border-border bg-secondary/20 py-24">
        <div className="mx-auto max-w-6xl px-6">
          <Reveal>
            <h2 className="font-display text-5xl tracking-tight md:text-6xl">INSIDE THE SHOP</h2>
            <p className="mt-4 text-muted-foreground">
              Click a screenshot to open it full size. Arrow keys and Esc work in the viewer.
            </p>
          </Reveal>
          <div className="mt-12 grid gap-8 md:grid-cols-2">
            {shots.map((s, i) => (
              <Reveal key={`${platform}-${s.label}`} delay={(i % 2) * 120} className={i === 0 ? "md:col-span-2" : ""}>
                <figure className="overflow-hidden rounded-md border border-border bg-card shadow-[0_30px_60px_-35px_rgba(0,0,0,0.9)]">
                  <button
                    type="button"
                    onClick={() => onOpenShot(i)}
                    className="m-0 block w-full cursor-zoom-in border-0 bg-transparent p-0 text-left"
                    aria-label={`View ${s.label} full size`}
                  >
                    <img
                      src={s.src}
                      alt={s.alt}
                      width={1782}
                      height={1221}
                      loading="lazy"
                      className="h-auto w-full transition-transform duration-700 hover:scale-[1.02]"
                    />
                  </button>
                  <figcaption className="border-t border-border px-5 py-3 text-xs uppercase tracking-[0.2em] text-muted-foreground">
                    {s.label}
                  </figcaption>
                </figure>
              </Reveal>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}

function Index() {
  const [platform, setPlatform] = useState<Platform>("windows");
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);

  const activeShots = platform === "windows" ? windowsShots : linuxShots;
  const heroSrc = platform === "windows" ? windowsHero : linuxHero;
  const heroAlt =
    platform === "windows"
      ? "SnowRunner Tuning Shop vehicle tuning panel with fuel tank, front steer, responsiveness, diff lock and drive settings"
      : "SnowRunner Tuning Shop Linux Avalonia vehicle tuning panel with fuel, steer, responsiveness, diff lock and drive settings";

  return (
    <main className="relative min-h-screen overflow-x-hidden bg-background font-sans text-foreground">
      {/* Hero */}
      <section className="relative isolate overflow-hidden aurora-bg grain-overlay">
        <div className="aurora-blob pointer-events-none absolute -left-40 top-0 h-[38rem] w-[38rem] rounded-full bg-accent/20 blur-[120px]" />
        <div className="aurora-blob pointer-events-none absolute -right-32 top-32 h-[30rem] w-[30rem] rounded-full bg-primary/15 blur-[120px]" />
        <Snowfall />

        <header className="relative z-10 mx-auto flex max-w-6xl items-center justify-between px-6 py-6">
          <span className="font-display text-2xl tracking-[0.18em] text-foreground">
            SNOWRUNNER <span className="text-primary">TUNING SHOP</span>
          </span>
          <a
            href={REPO_URL}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 rounded-sm border border-border/80 bg-card/60 px-4 py-2 text-sm font-medium backdrop-blur transition-colors hover:border-primary hover:text-primary"
          >
            <Github className="size-4" aria-hidden />
            Source
          </a>
        </header>

        <div className="relative z-10 mx-auto max-w-6xl px-6 pb-16 pt-14 md:pb-20 md:pt-20">
          <Reveal>
            <p className="mb-5 inline-flex items-center gap-2 rounded-full border border-primary/40 bg-primary/10 px-4 py-1.5 text-xs font-semibold uppercase tracking-[0.22em] text-primary">
              Free · Open source
            </p>
          </Reveal>
          <Reveal delay={80}>
            <h1 className="font-display text-6xl leading-[0.92] tracking-tight sm:text-7xl md:text-8xl">
              <span className="shine-text">REWRITE THE MUD</span>
              <br />
              <span className="text-muted-foreground">BEFORE YOU DRIVE IT</span>
            </h1>
          </Reveal>
          <Reveal delay={160}>
            <p className="mt-7 max-w-2xl text-lg leading-relaxed text-muted-foreground">
              SnowRunner Tuning Shop opens the game's <code className="rounded-sm bg-card px-1.5 py-0.5 text-ice">initial.pak</code> and lets
              you tune what the developers hard-coded — from a single engine's torque curve to the diff lock the
              truck was never given.
            </p>
          </Reveal>
          <Reveal delay={240}>
            <div className="mt-10 flex flex-wrap items-center gap-4">
              <a
                href={RELEASE_LATEST_URL}
                target="_blank"
                rel="noreferrer"
                style={{ animation: "pulse-ring 2.8s ease-out infinite" }}
                className="group inline-flex items-center gap-3 rounded-sm bg-primary px-8 py-4 font-display text-xl tracking-[0.12em] text-primary-foreground transition-transform hover:scale-[1.03]"
              >
                <Download className="size-5 transition-transform group-hover:translate-y-0.5" aria-hidden />
                DOWNLOAD LATEST
              </a>
              <a
                href={REPO_URL}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground underline-offset-4 transition-colors hover:text-ice hover:underline"
              >
                Read the code on GitHub <ArrowRight className="size-4" aria-hidden />
              </a>
            </div>
            <BadgeRow className="mt-5" />
          </Reveal>
        </div>
      </section>

      {/* Platform tabs: main shot, stats, features, gallery */}
      <section className="border-t border-border bg-background">
        <div className="mx-auto max-w-6xl px-6 pt-10">
          <Reveal>
            <div
              role="tablist"
              aria-label="Platform"
              className="inline-flex rounded-sm border border-border bg-card/50 p-1"
            >
              {(
                [
                  ["windows", "Windows"],
                  ["linux", "Linux"],
                ] as const
              ).map(([id, label]) => {
                const selected = platform === id;
                return (
                  <button
                    key={id}
                    type="button"
                    role="tab"
                    aria-selected={selected}
                    id={`platform-tab-${id}`}
                    onClick={() => {
                      setPlatform(id);
                      setLightboxIndex(null);
                    }}
                    className={`rounded-sm px-5 py-2.5 font-display text-sm tracking-[0.16em] transition-colors ${
                      selected
                        ? "bg-primary text-primary-foreground"
                        : "text-muted-foreground hover:text-foreground"
                    }`}
                  >
                    {label}
                  </button>
                );
              })}
            </div>
          </Reveal>
        </div>

        <div
          role="tabpanel"
          aria-labelledby={`platform-tab-${platform}`}
          className="pt-10"
        >
          <PlatformShowcase
            platform={platform}
            shots={activeShots}
            heroSrc={heroSrc}
            heroAlt={heroAlt}
            onOpenShot={setLightboxIndex}
          />
        </div>
      </section>

      {/* How it works — shared */}
      <section className="mx-auto max-w-6xl px-6 py-24">
        <Reveal>
          <h2 className="font-display text-5xl tracking-tight md:text-6xl">THREE STEPS</h2>
        </Reveal>
        <ol className="mt-12 grid gap-8 md:grid-cols-3">
          {[
            ["01", "Point it at the pak", "Steam, Epic or Xbox install — pick the folder once and the tool remembers it."],
            ["02", "Tune", "Global multipliers for whole part classes, or surgical per-vehicle edits."],
            ["03", "Drive or restore", "Save changes and launch. Anything you regret goes back to baseline in one click."],
          ].map(([n, t, b], i) => (
            <Reveal key={n} delay={i * 120}>
              <li className="relative border-l-2 border-primary/40 pl-6">
                <span className="font-display text-4xl text-primary/70">{n}</span>
                <h3 className="mt-2 font-display text-2xl tracking-wide">{t}</h3>
                <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{b}</p>
              </li>
            </Reveal>
          ))}
        </ol>
      </section>

      {/* CTA — shared */}
      <section className="relative isolate overflow-hidden aurora-bg grain-overlay border-t border-border">
        <Snowfall />
        <div className="relative z-10 mx-auto max-w-3xl px-6 py-28 text-center">
          <Reveal>
            <h2 className="font-display text-5xl tracking-tight md:text-7xl">
              <span className="shine-text">GET THE TUNING SHOP</span>
            </h2>
            <p className="mt-5 text-muted-foreground">
              Stable {APP_VERSION_STABLE} · Beta {APP_VERSION_BETA} · free forever · no account, no telemetry.
            </p>
            <div className="mt-10 flex flex-wrap items-center justify-center gap-4">
              <a
                href={RELEASE_LATEST_URL}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-3 rounded-sm bg-primary px-7 py-4 font-display text-lg tracking-[0.12em] text-primary-foreground transition-transform hover:scale-[1.03]"
              >
                <Download className="size-5" aria-hidden />
                WINDOWS INSTALLER
              </a>
              <a
                href={RELEASES_URL}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-3 rounded-sm border border-primary/50 bg-card/70 px-7 py-4 font-display text-lg tracking-[0.12em] text-foreground backdrop-blur transition-transform hover:scale-[1.03] hover:border-primary"
              >
                <Download className="size-5" aria-hidden />
                LINUX FLATPAK
              </a>
            </div>
            <p className="mt-4 text-xs text-muted-foreground">
              Grab the Windows Setup from the latest Stable release, or the{" "}
              <code className="rounded-sm bg-card px-1 py-0.5 text-ice">.flatpak</code> asset from Stable/Beta on{" "}
              <a href={RELEASES_URL} target="_blank" rel="noreferrer" className="underline-offset-2 hover:text-ice hover:underline">
                GitHub Releases
              </a>
              .
            </p>
            <BadgeRow className="mt-6 justify-center" />
          </Reveal>
        </div>
      </section>

      <footer className="border-t border-border bg-background">
        <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 px-6 py-8 text-sm text-muted-foreground md:flex-row">
          <p>
            SnowRunner Tuning Shop — an unofficial, fan-made tool. Not affiliated with Saber Interactive or Focus
            Entertainment.
          </p>
          <a
            href={REPO_URL}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 transition-colors hover:text-primary"
          >
            <Github className="size-4" aria-hidden /> Gixx/snowrunner-tuning-shop
          </a>
        </div>
      </footer>
      <Lightbox
        shots={activeShots}
        index={lightboxIndex}
        onClose={() => setLightboxIndex(null)}
        onIndexChange={setLightboxIndex}
      />
    </main>
  );
}
