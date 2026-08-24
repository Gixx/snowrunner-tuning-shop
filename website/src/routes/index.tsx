import { createFileRoute } from "@tanstack/react-router";
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
} from "lucide-react";

import { Snowfall } from "@/components/Snowfall";
import { Reveal } from "@/components/Reveal";
import shotHome from "@/assets/shot-193429.png";
import shotGeneral from "@/assets/shot-193436.png";
import shotParts from "@/assets/shot-193445.png";
import shotVehicles from "@/assets/shot-193453.png";
import shotVehicle from "@/assets/shot-193501.png";

const RELEASE_URL = "https://github.com/Gixx/snowrunner-tuning-shop/releases/latest";
const REPO_URL = "https://github.com/Gixx/snowrunner-tuning-shop";

const badges = [
  {
    href: RELEASE_URL,
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
    href: RELEASE_URL,
    src: "https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square&logo=windows&logoColor=white",
    alt: "Windows",
  },
  {
    href: RELEASE_URL,
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
      { title: "SnowRunner Tuning Shop — Tune the Game's Default Settings" },
      {
        name: "description",
        content:
          "Free, open-source desktop tool to fine-tune SnowRunner's initial.pak: engines, gearboxes, suspensions, fuel consumption, steering, AWD and diff lock — with a one-click baseline restore.",
      },
      { property: "og:title", content: "SnowRunner Tuning Shop — Tune the Game's Defaults" },
      {
        property: "og:description",
        content:
          "Open-source tweaker for SnowRunner: parts, vehicles, fuel, steering, AWD and diff lock. Safe baseline backup and restore.",
      },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary_large_image" },
    ],
  }),
  component: Index,
});

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
    body: "Front steer angle and how quickly the wheel snaps back to center — turn a barge into something that actually corners.",
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
    title: "Base game + 48 DLC packs",
    body: "Reads all 11 500+ XML entries, 116 vehicles and every DLC package straight out of the archive.",
  },
];

const shots = [
  { src: shotHome, alt: "SnowRunner Tuning Shop home screen showing baseline status and pak overview", label: "Home — baseline status" },
  { src: shotParts, alt: "Engine list with global torque, fuel and responsiveness multipliers", label: "Parts — engines" },
  { src: shotVehicles, alt: "Grid of 116 SnowRunner vehicles filtered by class", label: "Vehicles — 116 trucks" },
  { src: shotVehicle, alt: "Vehicle tuning panel for the AVENHORN A15 with fuel tank, steering and diff lock", label: "Vehicle tuning" },
  { src: shotGeneral, alt: "General tuning with camera collision mode and trail rock size slider", label: "General tuning" },
];

function Index() {
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

        <div className="relative z-10 mx-auto max-w-6xl px-6 pb-24 pt-14 md:pb-32 md:pt-20">
          <Reveal>
            <p className="mb-5 inline-flex items-center gap-2 rounded-full border border-primary/40 bg-primary/10 px-4 py-1.5 text-xs font-semibold uppercase tracking-[0.22em] text-primary">
              Free · Open source · Windows
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
                href={RELEASE_URL}
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

          <Reveal delay={320}>
            <div className="mt-16 overflow-hidden rounded-md border border-border bg-card/70 shadow-[0_40px_80px_-30px_rgba(0,0,0,0.9)] backdrop-blur">
              <img
                src={shotVehicle}
                alt="SnowRunner Tuning Shop vehicle tuning panel with fuel tank, front steer, responsiveness, diff lock and drive settings"
                width={1782}
                height={1161}
                className="w-full"
              />
            </div>
          </Reveal>
        </div>
      </section>

      {/* Stats */}
      <section className="border-y border-border bg-card/40">
        <div className="mx-auto grid max-w-6xl grid-cols-2 gap-px px-6 md:grid-cols-4">
          {[
            ["116", "vehicles"],
            ["125", "engines"],
            ["214", "suspensions"],
            ["48", "DLC packs"],
          ].map(([n, l], i) => (
            <Reveal key={l} delay={i * 90}>
              <div className="py-10 text-center">
                <div className="font-display text-5xl text-primary">{n}</div>
                <div className="mt-1 text-xs uppercase tracking-[0.24em] text-muted-foreground">{l}</div>
              </div>
            </Reveal>
          ))}
        </div>
      </section>

      {/* Features */}
      <section className="mx-auto max-w-6xl px-6 py-24">
        <Reveal>
          <h2 className="font-display text-5xl tracking-tight md:text-6xl">WHAT YOU CAN TUNE</h2>
          <p className="mt-4 max-w-2xl text-muted-foreground">
            No text editors, no XML diffing, no broken saves. Point it at your install and start turning knobs.
          </p>
        </Reveal>
        <div className="mt-14 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {features.map((f, i) => (
            <Reveal key={f.title} delay={(i % 3) * 100}>
              <article className="group h-full rounded-md border border-border bg-card/60 p-7 transition-colors duration-300 hover:border-primary/60 hover:bg-card">
                <f.icon className="size-7 text-ice transition-colors group-hover:text-primary" aria-hidden />
                <h3 className="mt-5 font-display text-2xl tracking-wide">{f.title}</h3>
                <p className="mt-3 text-sm leading-relaxed text-muted-foreground">{f.body}</p>
              </article>
            </Reveal>
          ))}
        </div>
      </section>

      {/* Gallery */}
      <section className="relative overflow-hidden border-y border-border bg-secondary/20 py-24">
        <div className="mx-auto max-w-6xl px-6">
          <Reveal>
            <h2 className="font-display text-5xl tracking-tight md:text-6xl">INSIDE THE SHOP</h2>
          </Reveal>
          <div className="mt-12 grid gap-8 md:grid-cols-2">
            {shots.map((s, i) => (
              <Reveal key={s.label} delay={(i % 2) * 120} className={i === 0 ? "md:col-span-2" : ""}>
                <figure className="overflow-hidden rounded-md border border-border bg-card shadow-[0_30px_60px_-35px_rgba(0,0,0,0.9)]">
                  <img
                    src={s.src}
                    alt={s.alt}
                    loading="lazy"
                    className="w-full transition-transform duration-700 hover:scale-[1.02]"
                  />
                  <figcaption className="border-t border-border px-5 py-3 text-xs uppercase tracking-[0.2em] text-muted-foreground">
                    {s.label}
                  </figcaption>
                </figure>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* How it works */}
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

      {/* CTA */}
      <section className="relative isolate overflow-hidden aurora-bg grain-overlay border-t border-border">
        <Snowfall />
        <div className="relative z-10 mx-auto max-w-3xl px-6 py-28 text-center">
          <Reveal>
            <h2 className="font-display text-5xl tracking-tight md:text-7xl">
              <span className="shine-text">GET THE TUNING SHOP</span>
            </h2>
            <p className="mt-5 text-muted-foreground">
              Version 1.1.2 · Windows installer · free forever · no account, no telemetry.
            </p>
            <a
              href={RELEASE_URL}
              target="_blank"
              rel="noreferrer"
              className="mt-10 inline-flex items-center gap-3 rounded-sm bg-primary px-9 py-4 font-display text-xl tracking-[0.12em] text-primary-foreground transition-transform hover:scale-[1.03]"
            >
              <Download className="size-5" aria-hidden />
              DOWNLOAD LATEST RELEASE
            </a>
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
    </main>
  );
}
