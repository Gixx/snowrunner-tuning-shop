import { createRouter } from "@tanstack/react-router";
import { routeTree } from "./routeTree.gen";

export const getRouter = () =>
  createRouter({
    routeTree,
    basepath: "/snowrunner-tuning-shop",
    scrollRestoration: true,
    defaultPreloadStaleTime: 0,
  });
