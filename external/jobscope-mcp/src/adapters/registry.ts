import { ashbyAdapter } from "./ashby";
import { greenhouseAdapter } from "./greenhouse";
import { leverAdapter } from "./lever";
import { workdayAdapter } from "./workday";
import { usajobsAdapter } from "./usajobs";
import type { AtsAdapter } from "./types";

export const adapters: AtsAdapter[] = [
  greenhouseAdapter,
  leverAdapter,
  ashbyAdapter,
  workdayAdapter,
  usajobsAdapter,
];

export function getAdapter(name: string): AtsAdapter | undefined {
  return adapters.find((a) => a.name === name);
}
